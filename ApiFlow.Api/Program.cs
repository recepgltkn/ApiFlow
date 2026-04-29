using ApiFlow.Api.Data;
using ApiFlow.Api.Models;
using ApiFlow.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpClient<ExternalApiClient>();
builder.Services.AddScoped<SqlServerPayloadWriter>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    await EnsureDatabaseSchemaAsync(dbContext);
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ApiFlow API v1");
});

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task EnsureDatabaseSchemaAsync(AppDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    await connection.OpenAsync();

    try
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(ApiProfiles);";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        if (!existingColumns.Contains("LoginPath"))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "ALTER TABLE ApiProfiles ADD COLUMN LoginPath TEXT DEFAULT NULL";
            await command.ExecuteNonQueryAsync();
        }

        if (!existingColumns.Contains("LoginHttpMethod"))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "ALTER TABLE ApiProfiles ADD COLUMN LoginHttpMethod TEXT NOT NULL DEFAULT 'POST'";
            await command.ExecuteNonQueryAsync();
        }

        if (!existingColumns.Contains("LoginBodyTemplate"))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "ALTER TABLE ApiProfiles ADD COLUMN LoginBodyTemplate TEXT DEFAULT NULL";
            await command.ExecuteNonQueryAsync();
        }

        if (!existingColumns.Contains("DefaultHeadersJson"))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "ALTER TABLE ApiProfiles ADD COLUMN DefaultHeadersJson TEXT DEFAULT NULL";
            await command.ExecuteNonQueryAsync();
        }

        if (!existingColumns.Contains("SessionIdJsonPath"))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "ALTER TABLE ApiProfiles ADD COLUMN SessionIdJsonPath TEXT DEFAULT NULL";
            await command.ExecuteNonQueryAsync();
        }

        if (!await TableExistsAsync(connection, "ApiEndpoints"))
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"CREATE TABLE ApiEndpoints (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProfileId INTEGER NOT NULL DEFAULT 0,
                Key TEXT NOT NULL,
                HttpMethod TEXT NOT NULL DEFAULT 'POST',
                RequestBodyTemplate TEXT,
                HeadersJson TEXT,
                ResultJsonPath TEXT,
                TargetTableName TEXT,
                CreateTableIfMissing INTEGER NOT NULL DEFAULT 1,
                AddMissingColumns INTEGER NOT NULL DEFAULT 1,
                ClearTableBeforeImport INTEGER NOT NULL DEFAULT 0,
                Path TEXT NOT NULL
            )";
            await command.ExecuteNonQueryAsync();
        }
        else
        {
            var endpointColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(ApiEndpoints);";
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    endpointColumns.Add(reader.GetString(1));
                }
            }

            if (!endpointColumns.Contains("HttpMethod"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE ApiEndpoints ADD COLUMN HttpMethod TEXT NOT NULL DEFAULT 'POST'";
                await command.ExecuteNonQueryAsync();
            }

            if (!endpointColumns.Contains("ProfileId"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE ApiEndpoints ADD COLUMN ProfileId INTEGER NOT NULL DEFAULT 0";
                await command.ExecuteNonQueryAsync();
            }

            if (!endpointColumns.Contains("RequestBodyTemplate"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE ApiEndpoints ADD COLUMN RequestBodyTemplate TEXT DEFAULT NULL";
                await command.ExecuteNonQueryAsync();
            }

            if (!endpointColumns.Contains("HeadersJson"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE ApiEndpoints ADD COLUMN HeadersJson TEXT DEFAULT NULL";
                await command.ExecuteNonQueryAsync();
            }

            if (!endpointColumns.Contains("ResultJsonPath"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE ApiEndpoints ADD COLUMN ResultJsonPath TEXT DEFAULT NULL";
                await command.ExecuteNonQueryAsync();
            }

            if (!endpointColumns.Contains("TargetTableName"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE ApiEndpoints ADD COLUMN TargetTableName TEXT DEFAULT NULL";
                await command.ExecuteNonQueryAsync();
            }

            if (!endpointColumns.Contains("CreateTableIfMissing"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE ApiEndpoints ADD COLUMN CreateTableIfMissing INTEGER NOT NULL DEFAULT 1";
                await command.ExecuteNonQueryAsync();
            }

            if (!endpointColumns.Contains("AddMissingColumns"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE ApiEndpoints ADD COLUMN AddMissingColumns INTEGER NOT NULL DEFAULT 1";
                await command.ExecuteNonQueryAsync();
            }

            if (!endpointColumns.Contains("ClearTableBeforeImport"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE ApiEndpoints ADD COLUMN ClearTableBeforeImport INTEGER NOT NULL DEFAULT 0";
                await command.ExecuteNonQueryAsync();
            }
        }

        await AssignExistingEndpointsToProfilesAsync(dbContext);

        if (!await TableExistsAsync(connection, "SqlProfiles"))
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"CREATE TABLE SqlProfiles (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Key TEXT NOT NULL,
                Name TEXT NOT NULL,
                Host TEXT NOT NULL,
                Port INTEGER,
                InstanceName TEXT,
                DatabaseName TEXT NOT NULL,
                Username TEXT NOT NULL,
                Password TEXT NOT NULL,
                ApplicationName TEXT,
                TrustServerCertificate INTEGER NOT NULL DEFAULT 1,
                Encrypt INTEGER NOT NULL DEFAULT 0,
                SchemaName TEXT NOT NULL DEFAULT 'dbo',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )";
            await command.ExecuteNonQueryAsync();
        }
        else
        {
            var sqlProfileColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(SqlProfiles);";
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    sqlProfileColumns.Add(reader.GetString(1));
                }
            }

            if (!sqlProfileColumns.Contains("SchemaName"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE SqlProfiles ADD COLUMN SchemaName TEXT NOT NULL DEFAULT 'dbo'";
                await command.ExecuteNonQueryAsync();
            }

            if (!sqlProfileColumns.Contains("Key"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE SqlProfiles ADD COLUMN Key TEXT NOT NULL DEFAULT ''";
                await command.ExecuteNonQueryAsync();
            }

            if (!sqlProfileColumns.Contains("Host"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE SqlProfiles ADD COLUMN Host TEXT NOT NULL DEFAULT ''";
                await command.ExecuteNonQueryAsync();
            }

            if (!sqlProfileColumns.Contains("Port"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE SqlProfiles ADD COLUMN Port INTEGER DEFAULT NULL";
                await command.ExecuteNonQueryAsync();
            }

            if (!sqlProfileColumns.Contains("InstanceName"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE SqlProfiles ADD COLUMN InstanceName TEXT DEFAULT NULL";
                await command.ExecuteNonQueryAsync();
            }

            if (!sqlProfileColumns.Contains("DatabaseName"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE SqlProfiles ADD COLUMN DatabaseName TEXT NOT NULL DEFAULT ''";
                await command.ExecuteNonQueryAsync();
            }

            if (!sqlProfileColumns.Contains("Username"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE SqlProfiles ADD COLUMN Username TEXT NOT NULL DEFAULT ''";
                await command.ExecuteNonQueryAsync();
            }

            if (!sqlProfileColumns.Contains("Password"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE SqlProfiles ADD COLUMN Password TEXT NOT NULL DEFAULT ''";
                await command.ExecuteNonQueryAsync();
            }

            if (!sqlProfileColumns.Contains("ApplicationName"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE SqlProfiles ADD COLUMN ApplicationName TEXT DEFAULT NULL";
                await command.ExecuteNonQueryAsync();
            }

            if (!sqlProfileColumns.Contains("TrustServerCertificate"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE SqlProfiles ADD COLUMN TrustServerCertificate INTEGER NOT NULL DEFAULT 1";
                await command.ExecuteNonQueryAsync();
            }

            if (!sqlProfileColumns.Contains("Encrypt"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE SqlProfiles ADD COLUMN Encrypt INTEGER NOT NULL DEFAULT 0";
                await command.ExecuteNonQueryAsync();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE SqlProfiles SET Key = lower(replace(Name, ' ', '-')) WHERE Key = '' OR Key IS NULL";
                await command.ExecuteNonQueryAsync();
            }

            if (sqlProfileColumns.Contains("TableName"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE SqlProfiles DROP COLUMN TableName";
                await command.ExecuteNonQueryAsync();
            }

            if (sqlProfileColumns.Contains("ConnectionString"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE SqlProfiles DROP COLUMN ConnectionString";
                await command.ExecuteNonQueryAsync();
            }
        }
    }
    finally
    {
        await connection.CloseAsync();
    }
}

static async Task AssignExistingEndpointsToProfilesAsync(AppDbContext dbContext)
{
    var endpoints = await dbContext.ApiEndpoints
        .Where(endpoint => endpoint.ProfileId == 0)
        .ToListAsync();

    if (endpoints.Count == 0)
    {
        return;
    }

    var profiles = await dbContext.ApiProfiles.ToListAsync();
    var defaultProfile = profiles.OrderBy(profile => profile.Id).FirstOrDefault();

    foreach (var endpoint in endpoints)
    {
        var matchedProfile = profiles.FirstOrDefault(profile =>
            endpoint.Key.StartsWith(profile.Name.Replace("_", "-"), StringComparison.OrdinalIgnoreCase));

        endpoint.ProfileId = matchedProfile?.Id ?? defaultProfile?.Id ?? 0;
    }

    await dbContext.SaveChangesAsync();
}

static async Task<bool> TableExistsAsync(DbConnection connection, string tableName)
{
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name = @name";
    var param = command.CreateParameter();
    param.ParameterName = "@name";
    param.Value = tableName;
    command.Parameters.Add(param);
    var result = await command.ExecuteScalarAsync();
    return result is not null;
}
