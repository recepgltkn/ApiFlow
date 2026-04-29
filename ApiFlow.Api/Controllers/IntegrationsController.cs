using ApiFlow.Api.Contracts;
using ApiFlow.Api.Data;
using ApiFlow.Api.Models;
using ApiFlow.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiFlow.Api.Controllers;

[ApiController]
[Route("api/integrations")]
public sealed class IntegrationsController(
    AppDbContext dbContext,
    ExternalApiClient apiClient,
    SqlServerPayloadWriter sqlServerPayloadWriter) : ControllerBase
{
    [HttpGet("operations")]
    public async Task<ActionResult<IReadOnlyCollection<ApiOperationInfo>>> GetOperations(
        [FromQuery] int? profileId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ApiEndpoints.AsNoTracking();
        if (profileId is not null)
        {
            query = query.Where(endpoint => endpoint.ProfileId == profileId);
        }

        var operations = await query
            .OrderBy(endpoint => endpoint.Key)
            .Select(endpoint => ToOperationInfo(endpoint))
            .ToArrayAsync(cancellationToken);

        return operations;
    }

    [HttpPost("operations")]
    public async Task<ActionResult<ApiOperationInfo>> CreateOperation(ApiEndpointRequest request, CancellationToken cancellationToken)
    {
        var key = request.Key.Trim();
        if (!await dbContext.ApiProfiles.AnyAsync(profile => profile.Id == request.ProfileId, cancellationToken))
        {
            return BadRequest(new { message = "Endpoint için geçerli bir profil seçilmeli." });
        }

        if (await dbContext.ApiEndpoints.AnyAsync(endpoint => endpoint.ProfileId == request.ProfileId && endpoint.Key == key, cancellationToken))
        {
            return Conflict(new { message = $"'{key}' anahtarlı endpoint zaten var." });
        }

        var endpoint = new ApiEndpoint
        {
            ProfileId = request.ProfileId,
            Key = key,
            HttpMethod = NormalizeMethod(request.HttpMethod),
            Path = request.Path.Trim(),
            RequestBodyTemplate = NormalizeOptional(request.RequestBodyTemplate),
            HeadersJson = NormalizeOptional(request.HeadersJson),
            ResultJsonPath = NormalizeOptional(request.ResultJsonPath),
            TargetTableName = NormalizeOptional(request.TargetTableName),
            CreateTableIfMissing = request.CreateTableIfMissing,
            AddMissingColumns = request.AddMissingColumns,
            ClearTableBeforeImport = request.ClearTableBeforeImport
        };

        dbContext.ApiEndpoints.Add(endpoint);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetOperations), ToOperationInfo(endpoint));
    }

    [HttpPut("operations/{operationKey}")]
    public async Task<ActionResult<ApiOperationInfo>> UpdateOperation(
        string operationKey,
        ApiEndpointRequest request,
        CancellationToken cancellationToken)
    {
        var endpoint = await dbContext.ApiEndpoints.FirstOrDefaultAsync(endpoint => endpoint.Key == operationKey, cancellationToken);
        if (endpoint is null)
        {
            return NotFound();
        }

        var key = request.Key.Trim();
        if (!await dbContext.ApiProfiles.AnyAsync(profile => profile.Id == request.ProfileId, cancellationToken))
        {
            return BadRequest(new { message = "Endpoint için geçerli bir profil seçilmeli." });
        }

        if (await dbContext.ApiEndpoints.AnyAsync(candidate => candidate.Id != endpoint.Id && candidate.ProfileId == request.ProfileId && candidate.Key == key, cancellationToken))
        {
            return Conflict(new { message = $"'{key}' anahtarlı endpoint zaten var." });
        }

        endpoint.ProfileId = request.ProfileId;
        endpoint.Key = key;
        endpoint.HttpMethod = NormalizeMethod(request.HttpMethod);
        endpoint.Path = request.Path.Trim();
        endpoint.RequestBodyTemplate = NormalizeOptional(request.RequestBodyTemplate);
        endpoint.HeadersJson = NormalizeOptional(request.HeadersJson);
        endpoint.ResultJsonPath = NormalizeOptional(request.ResultJsonPath);
        endpoint.TargetTableName = NormalizeOptional(request.TargetTableName);
        endpoint.CreateTableIfMissing = request.CreateTableIfMissing;
        endpoint.AddMissingColumns = request.AddMissingColumns;
        endpoint.ClearTableBeforeImport = request.ClearTableBeforeImport;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToOperationInfo(endpoint);
    }

    [HttpDelete("operations/{operationKey}")]
    public async Task<IActionResult> DeleteOperation(string operationKey, CancellationToken cancellationToken)
    {
        var deleted = await dbContext.ApiEndpoints
            .Where(endpoint => endpoint.Key == operationKey)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? NotFound() : NoContent();
    }

    [HttpPost("profiles/{profileId:int}/login")]
    public async Task<ActionResult<ApiLoginResponse>> Login(int profileId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.ApiProfiles.FirstOrDefaultAsync(profile => profile.Id == profileId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        ApiLoginResponse login;
        try
        {
            login = await apiClient.LoginAsync(profile, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        profile.LastSessionId = login.SessionId;
        profile.LastLoginAt = DateTimeOffset.UtcNow;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return login;
    }

    [HttpPost("profiles/{profileId:int}/operations/{operationKey}")]
    public async Task<ActionResult<ApiExecutionResponse>> ExecuteOperation(
        int profileId,
        string operationKey,
        ApiExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var operation = await dbContext.ApiEndpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(endpoint => endpoint.ProfileId == profileId && endpoint.Key == operationKey, cancellationToken);

        if (operation is null)
        {
            var availableOperations = await dbContext.ApiEndpoints
                .AsNoTracking()
                .Where(endpoint => endpoint.ProfileId == profileId)
                .OrderBy(endpoint => endpoint.Key)
                .Select(endpoint => endpoint.Key)
                .ToArrayAsync(cancellationToken);

            return BadRequest(new
            {
                message = "Bilinmeyen endpoint.",
                availableOperations
            });
        }

        var profile = await dbContext.ApiProfiles.FirstOrDefaultAsync(profile => profile.Id == profileId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        var sessionId = profile.LastSessionId;
        if (!string.IsNullOrWhiteSpace(profile.LoginPath) && (request.ForceLogin || string.IsNullOrWhiteSpace(sessionId)))
        {
            ApiLoginResponse login;
            try
            {
                login = await apiClient.LoginAsync(profile, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            sessionId = login.SessionId;
            profile.LastSessionId = login.SessionId;
            profile.LastLoginAt = DateTimeOffset.UtcNow;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var operationInfo = ToOperationInfo(operation);
        var result = await apiClient.ExecuteAsync(profile, operationInfo, request, sessionId, cancellationToken);
        if (IsInvalidSession(result) && !string.IsNullOrWhiteSpace(profile.LoginPath))
        {
            ApiLoginResponse login;
            try
            {
                login = await apiClient.LoginAsync(profile, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            sessionId = login.SessionId;
            profile.LastSessionId = login.SessionId;
            profile.LastLoginAt = DateTimeOffset.UtcNow;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            result = await apiClient.ExecuteAsync(profile, operationInfo, request, sessionId, cancellationToken);
        }

        var savedRows = 0;
        if (request.SaveToSqlServer)
        {
            if (request.SqlProfileId is null)
            {
                return BadRequest(new { message = "MSSQL'e yazmak için SQL profili seçilmeli." });
            }

            var sqlProfile = await dbContext.SqlProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(profile => profile.Id == request.SqlProfileId, cancellationToken);

            if (sqlProfile is null)
            {
                return BadRequest(new { message = "Seçilen SQL profili bulunamadı." });
            }

            savedRows = await sqlServerPayloadWriter.SaveAsync(sqlProfile, profile, operationInfo, result, cancellationToken);
        }

        return new ApiExecutionResponse(result, savedRows);
    }

    private static ApiOperationInfo ToOperationInfo(ApiEndpoint endpoint)
    {
        return new ApiOperationInfo(
            endpoint.ProfileId,
            endpoint.Key,
            endpoint.HttpMethod,
            endpoint.Path,
            endpoint.RequestBodyTemplate,
            endpoint.HeadersJson,
            endpoint.ResultJsonPath,
            endpoint.TargetTableName,
            endpoint.CreateTableIfMissing,
            endpoint.AddMissingColumns,
            endpoint.ClearTableBeforeImport);
    }

    private static string NormalizeMethod(string method)
    {
        return method.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsInvalidSession(System.Text.Json.JsonElement result)
    {
        if (result.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return false;
        }

        if (result.TryGetProperty("code", out var code) &&
            result.TryGetProperty("msg", out var msg) &&
            code.GetString() == "401" &&
            string.Equals(msg.GetString(), "INVALID_SESSION", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return result.TryGetProperty("data", out var data) && IsInvalidSession(data);
    }
}
