using System.Data;
using System.Text.Json;
using ApiFlow.Api.Contracts;
using ApiFlow.Api.Models;
using Microsoft.Data.SqlClient;

namespace ApiFlow.Api.Services;

public sealed class SqlServerPayloadWriter(IConfiguration configuration)
{
    private readonly string? connectionString = configuration.GetConnectionString("DestinationSqlServer");

    public async Task<int> SaveAsync(
        ApiProfile profile,
        ApiOperationInfo operation,
        JsonElement apiResponse,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("DestinationSqlServer connection string tanımlı değil.");
        }

        var rows = ExtractResultRows(apiResponse, operation.ResultJsonPath).ToArray();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);

        if (rows.Length == 0)
        {
            return 0;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var saved = 0;
            foreach (var row in rows)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = (SqlTransaction)transaction;
                command.CommandText = """
                    INSERT INTO dbo.ApiPayloads
                        (ProfileId, ProfileName, OperationKey, HttpMethod, RowKey, PayloadJson, ReceivedAt)
                    VALUES
                        (@ProfileId, @ProfileName, @OperationKey, @HttpMethod, @RowKey, @PayloadJson, SYSUTCDATETIME());
                    """;

                command.Parameters.Add(new SqlParameter("@ProfileId", SqlDbType.Int) { Value = profile.Id });
                command.Parameters.Add(new SqlParameter("@ProfileName", SqlDbType.NVarChar, 100) { Value = profile.Name });
                command.Parameters.Add(new SqlParameter("@OperationKey", SqlDbType.NVarChar, 100) { Value = operation.Key });
                command.Parameters.Add(new SqlParameter("@HttpMethod", SqlDbType.NVarChar, 150) { Value = operation.HttpMethod });
                command.Parameters.Add(new SqlParameter("@RowKey", SqlDbType.NVarChar, 100) { Value = GetRowKey(row) });
                command.Parameters.Add(new SqlParameter("@PayloadJson", SqlDbType.NVarChar, -1) { Value = row.GetRawText() });

                saved += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return saved;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task EnsureTableAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID(N'dbo.ApiPayloads', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ApiPayloads
                (
                    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApiPayloads PRIMARY KEY,
                    ProfileId int NOT NULL,
                    ProfileName nvarchar(100) NOT NULL,
                    OperationKey nvarchar(100) NOT NULL,
                    HttpMethod nvarchar(150) NOT NULL,
                    RowKey nvarchar(100) NULL,
                    PayloadJson nvarchar(max) NOT NULL,
                    ReceivedAt datetime2(3) NOT NULL CONSTRAINT DF_ApiPayloads_ReceivedAt DEFAULT SYSUTCDATETIME()
                );

                CREATE INDEX IX_ApiPayloads_Profile_Operation
                    ON dbo.ApiPayloads(ProfileId, OperationKey, ReceivedAt);
            END
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IEnumerable<JsonElement> ExtractResultRows(JsonElement apiResponse, string? resultJsonPath)
    {
        var result = ResolvePath(apiResponse, resultJsonPath) ?? ResolvePath(apiResponse, "result") ?? apiResponse;

        if (result.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in result.EnumerateArray())
            {
                yield return item.Clone();
            }
        }
        else if (result.ValueKind == JsonValueKind.Object)
        {
            yield return result.Clone();
        }
    }

    private static JsonElement? ResolvePath(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current;
    }

    private static object GetRowKey(JsonElement row)
    {
        if (row.ValueKind == JsonValueKind.Object &&
            (row.TryGetProperty("_key", out var key) || row.TryGetProperty("id", out key)))
        {
            return key.ValueKind switch
            {
                JsonValueKind.Number => key.GetRawText(),
                JsonValueKind.String => (object?)key.GetString() ?? DBNull.Value,
                _ => key.GetRawText()
            };
        }

        return DBNull.Value;
    }
}
