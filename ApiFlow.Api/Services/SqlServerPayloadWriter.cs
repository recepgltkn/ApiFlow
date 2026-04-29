using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApiFlow.Api.Contracts;
using ApiFlow.Api.Models;
using Microsoft.Data.SqlClient;

namespace ApiFlow.Api.Services;

public sealed partial class SqlServerPayloadWriter
{
    public async Task<int> SaveAsync(
        SqlProfile sqlProfile,
        ApiProfile apiProfile,
        ApiOperationInfo operation,
        JsonElement apiResponse,
        CancellationToken cancellationToken)
    {
        var rows = ExtractResultRows(apiResponse, operation.ResultJsonPath).ToArray();
        if (rows.Length == 0)
        {
            return 0;
        }

        await using var connection = new SqlConnection(BuildConnectionString(sqlProfile));
        await connection.OpenAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(operation.TargetTableName))
        {
            throw new InvalidOperationException("Endpoint için hedef MSSQL tablo adı tanımlı değil.");
        }

        var schemaName = SanitizeIdentifier(sqlProfile.SchemaName);
        var tableName = SanitizeIdentifier(operation.TargetTableName);
        await EnsureSchemaAsync(connection, schemaName, cancellationToken);
        if (operation.CreateTableIfMissing)
        {
            await EnsureTableAsync(connection, schemaName, tableName, cancellationToken);
        }

        var rowDictionaries = rows.Select(row => ToColumnValues(row, apiProfile, operation)).ToArray();
        var columns = rowDictionaries
            .SelectMany(row => row.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(SanitizeIdentifier)
            .ToArray();

        if (operation.AddMissingColumns)
        {
            await EnsureColumnsAsync(connection, schemaName, tableName, columns, cancellationToken);
        }

        if (operation.ClearTableBeforeImport)
        {
            await ClearTableAsync(connection, schemaName, tableName, cancellationToken);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var saved = 0;
            foreach (var row in rowDictionaries)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = (SqlTransaction)transaction;

                var columnList = string.Join(", ", columns.Select(QuoteIdentifier));
                var parameterList = string.Join(", ", columns.Select((_, index) => $"@p{index}"));
                command.CommandText = $"INSERT INTO {QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)} ({columnList}) VALUES ({parameterList});";

                for (var i = 0; i < columns.Length; i++)
                {
                    row.TryGetValue(columns[i], out var value);
                    command.Parameters.Add(new SqlParameter($"@p{i}", SqlDbType.NVarChar, -1)
                    {
                        Value = string.IsNullOrEmpty(value) ? DBNull.Value : value
                    });
                }

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

    private static async Task EnsureSchemaAsync(SqlConnection connection, string schemaName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF SCHEMA_ID(@schemaName) IS NULL
                EXEC(N'CREATE SCHEMA {QuoteIdentifier(schemaName)}');
            """;
        command.Parameters.Add(new SqlParameter("@schemaName", SqlDbType.NVarChar, 128) { Value = schemaName });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureTableAsync(SqlConnection connection, string schemaName, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF OBJECT_ID(@fullName, N'U') IS NULL
                CREATE TABLE {QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}
                (
                    [Id] bigint IDENTITY(1,1) NOT NULL CONSTRAINT {QuoteIdentifier($"PK_{tableName}")} PRIMARY KEY,
                    [ReceivedAt] nvarchar(max) NULL
                );
            """;
        command.Parameters.Add(new SqlParameter("@fullName", SqlDbType.NVarChar, 300) { Value = $"{schemaName}.{tableName}" });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureColumnsAsync(
        SqlConnection connection,
        string schemaName,
        string tableName,
        IReadOnlyCollection<string> columns,
        CancellationToken cancellationToken)
    {
        foreach (var column in columns.Where(column => !column.Equals("Id", StringComparison.OrdinalIgnoreCase)))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                IF COL_LENGTH(@fullName, @columnName) IS NULL
                    ALTER TABLE {QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}
                    ADD {QuoteIdentifier(column)} nvarchar(max) NULL;
                """;
            command.Parameters.Add(new SqlParameter("@fullName", SqlDbType.NVarChar, 300) { Value = $"{schemaName}.{tableName}" });
            command.Parameters.Add(new SqlParameter("@columnName", SqlDbType.NVarChar, 128) { Value = column });
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ClearTableAsync(SqlConnection connection, string schemaName, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)};";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Dictionary<string, string?> ToColumnValues(JsonElement row, ApiProfile apiProfile, ApiOperationInfo operation)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ReceivedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["ApiProfileName"] = apiProfile.Name,
            ["OperationKey"] = operation.Key
        };

        if (row.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in row.EnumerateObject())
            {
                values[SanitizeIdentifier(property.Name)] = ToSqlValue(property.Value);
            }
        }
        else
        {
            values["Payload"] = row.GetRawText();
        }

        return values;
    }

    private static string? ToSqlValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.GetRawText()
        };
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

    private static string QuoteIdentifier(string value)
    {
        return $"[{value.Replace("]", "]]")}]";
    }

    private static string BuildConnectionString(SqlProfile profile)
    {
        var server = profile.Host.Trim();
        if (!string.IsNullOrWhiteSpace(profile.InstanceName))
        {
            server += "\\" + profile.InstanceName.Trim();
        }

        if (profile.Port is > 0)
        {
            server += "," + profile.Port.Value;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = profile.DatabaseName,
            UserID = profile.Username,
            Password = profile.Password,
            TrustServerCertificate = profile.TrustServerCertificate,
            Encrypt = profile.Encrypt
        };
        if (!string.IsNullOrWhiteSpace(profile.ApplicationName))
        {
            builder.ApplicationName = profile.ApplicationName;
        }

        return builder.ConnectionString;
    }

    private static string SanitizeIdentifier(string value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? "Column" : value.Trim();
        var safe = InvalidIdentifierCharacters().Replace(trimmed, "_");
        if (char.IsDigit(safe[0]))
        {
            safe = "_" + safe;
        }

        return safe.Length <= 128 ? safe : safe[..128];
    }

    [GeneratedRegex("[^A-Za-z0-9_]+")]
    private static partial Regex InvalidIdentifierCharacters();
}
