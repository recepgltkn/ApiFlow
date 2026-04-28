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
                Key TEXT NOT NULL,
                HttpMethod TEXT NOT NULL DEFAULT 'POST',
                RequestBodyTemplate TEXT,
                HeadersJson TEXT,
                ResultJsonPath TEXT,
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
        }
    }
    finally
    {
        await connection.CloseAsync();
    }
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
