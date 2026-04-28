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
        DiaOperationInfo operation,
        JsonElement diaResponse,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("DestinationSqlServer connection string tanımlı değil.");
        }

        var rows = ExtractResultRows(diaResponse).ToArray();
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
                    INSERT INTO dbo.DiaApiPayloads
                        (ProfileId, ProfileName, OperationKey, DiaMethod, DiaKey, PayloadJson, ReceivedAt)
                    VALUES
                        (@ProfileId, @ProfileName, @OperationKey, @DiaMethod, @DiaKey, @PayloadJson, SYSUTCDATETIME());
                    """;

                command.Parameters.Add(new SqlParameter("@ProfileId", SqlDbType.Int) { Value = profile.Id });
                command.Parameters.Add(new SqlParameter("@ProfileName", SqlDbType.NVarChar, 100) { Value = profile.Name });
                command.Parameters.Add(new SqlParameter("@OperationKey", SqlDbType.NVarChar, 100) { Value = operation.Key });
                command.Parameters.Add(new SqlParameter("@DiaMethod", SqlDbType.NVarChar, 150) { Value = operation.Method });
                command.Parameters.Add(new SqlParameter("@DiaKey", SqlDbType.NVarChar, 100) { Value = GetDiaKey(row) });
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
            IF OBJECT_ID(N'dbo.DiaApiPayloads', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.DiaApiPayloads
                (
                    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DiaApiPayloads PRIMARY KEY,
                    ProfileId int NOT NULL,
                    ProfileName nvarchar(100) NOT NULL,
                    OperationKey nvarchar(100) NOT NULL,
                    DiaMethod nvarchar(150) NOT NULL,
                    DiaKey nvarchar(100) NULL,
                    PayloadJson nvarchar(max) NOT NULL,
                    ReceivedAt datetime2(3) NOT NULL CONSTRAINT DF_DiaApiPayloads_ReceivedAt DEFAULT SYSUTCDATETIME()
                );

                CREATE INDEX IX_DiaApiPayloads_Profile_Operation
                    ON dbo.DiaApiPayloads(ProfileId, OperationKey, ReceivedAt);
            END
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IEnumerable<JsonElement> ExtractResultRows(JsonElement diaResponse)
    {
        if (diaResponse.ValueKind == JsonValueKind.Object &&
            diaResponse.TryGetProperty("result", out var result) &&
            result.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in result.EnumerateArray())
            {
                yield return item.Clone();
            }
        }
    }

    private static object GetDiaKey(JsonElement row)
    {
        if (row.ValueKind == JsonValueKind.Object &&
            row.TryGetProperty("_key", out var key))
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
