using ApiFlow.Api.Contracts;
using ApiFlow.Api.Data;
using ApiFlow.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiFlow.Api.Controllers;

[ApiController]
[Route("api/dia")]
public sealed class DiaController(
    AppDbContext dbContext,
    DiaApiClient diaApiClient,
    SqlServerPayloadWriter sqlServerPayloadWriter) : ControllerBase
{
    [HttpGet("operations")]
    public async Task<ActionResult<IReadOnlyCollection<DiaOperationInfo>>> GetOperations(CancellationToken cancellationToken)
    {
        var operations = await dbContext.ApiEndpoints
            .AsNoTracking()
            .OrderBy(endpoint => endpoint.Key)
            .Select(endpoint => new DiaOperationInfo(endpoint.Key, endpoint.HttpMethod, endpoint.Path, endpoint.RequestBodyTemplate))
            .ToArrayAsync(cancellationToken);

        return operations;
    }

    [HttpPost("profiles/{profileId:int}/login")]
    public async Task<ActionResult<DiaLoginResponse>> Login(int profileId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.ApiProfiles.FirstOrDefaultAsync(profile => profile.Id == profileId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        var login = await diaApiClient.LoginAsync(profile, cancellationToken);
        profile.LastSessionId = login.SessionId;
        profile.LastLoginAt = DateTimeOffset.UtcNow;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return login;
    }

    [HttpPost("profiles/{profileId:int}/operations/{operationKey}")]
    public async Task<ActionResult<DiaOperationResponse>> ExecuteOperation(
        int profileId,
        string operationKey,
        DiaListRequest request,
        CancellationToken cancellationToken)
    {
        var operation = await dbContext.ApiEndpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(endpoint => endpoint.Key == operationKey, cancellationToken);

        if (operation is null)
        {
            var availableOperations = await dbContext.ApiEndpoints
                .AsNoTracking()
                .OrderBy(endpoint => endpoint.Key)
                .Select(endpoint => endpoint.Key)
                .ToArrayAsync(cancellationToken);

            return BadRequest(new
            {
                message = "Bilinmeyen operasyon.",
                availableOperations
            });
        }

        var profile = await dbContext.ApiProfiles.FirstOrDefaultAsync(profile => profile.Id == profileId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        var sessionId = profile.LastSessionId;
        if (request.ForceLogin || string.IsNullOrWhiteSpace(sessionId))
        {
            var login = await diaApiClient.LoginAsync(profile, cancellationToken);
            sessionId = login.SessionId;
            profile.LastSessionId = login.SessionId;
            profile.LastLoginAt = DateTimeOffset.UtcNow;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var operationInfo = new DiaOperationInfo(operation.Key, operation.HttpMethod, operation.Path, operation.RequestBodyTemplate);
        var result = await diaApiClient.ExecuteListAsync(profile, operationInfo, request, sessionId!, cancellationToken);
        var savedRows = request.SaveToSqlServer
            ? await sqlServerPayloadWriter.SaveAsync(profile, operationInfo, result, cancellationToken)
            : 0;

        return new DiaOperationResponse(result, savedRows);
    }
}
