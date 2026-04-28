using ApiFlow.Api.Contracts;
using ApiFlow.Api.Data;
using ApiFlow.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiFlow.Api.Controllers;

[ApiController]
[Route("api/profiles")]
public sealed class ApiProfilesController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApiProfileResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var profiles = await dbContext.ApiProfiles
            .AsNoTracking()
            .OrderBy(profile => profile.Name)
            .Select(profile => ToResponse(profile))
            .ToListAsync(cancellationToken);

        return profiles;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiProfileResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var profile = await dbContext.ApiProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

        return profile is null ? NotFound() : ToResponse(profile);
    }

    [HttpPost]
    public async Task<ActionResult<ApiProfileResponse>> Create(ApiProfileRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await dbContext.ApiProfiles.AnyAsync(profile => profile.Name == name, cancellationToken))
        {
            return Conflict(new { message = $"'{name}' adında bir profil zaten var." });
        }

        var normalizedBaseUrl = NormalizeBaseUrl(request.BaseUrl);

        var profile = new ApiProfile
        {
            Name = name,
            Username = request.Username.Trim(),
            Password = request.Password,
            ApiKey = request.ApiKey.Trim(),
            LoginPath = NormalizeOptional(request.LoginPath),
            LoginHttpMethod = request.LoginHttpMethod,
            LoginBodyTemplate = request.LoginBodyTemplate,
            DefaultHeadersJson = NormalizeOptional(request.DefaultHeadersJson),
            SessionIdJsonPath = NormalizeOptional(request.SessionIdJsonPath),
            BaseUrl = normalizedBaseUrl,
            Language = request.Language.Trim(),
            DisconnectSameUser = request.DisconnectSameUser,
            FirmaKodu = request.FirmaKodu,
            DonemKodu = request.DonemKodu
        };

        dbContext.ApiProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = ToResponse(profile);
        return CreatedAtAction(nameof(GetById), new { id = profile.Id }, response);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiProfileResponse>> Update(int id, ApiProfileUpdateRequest request, CancellationToken cancellationToken)
    {
        var profile = await dbContext.ApiProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();
        if (await dbContext.ApiProfiles.AnyAsync(profile => profile.Id != id && profile.Name == name, cancellationToken))
        {
            return Conflict(new { message = $"'{name}' adında bir profil zaten var." });
        }

        var normalizedBaseUrl = NormalizeBaseUrl(request.BaseUrl);

        profile.Name = name;
        profile.Username = request.Username.Trim();
        profile.Password = request.Password;
        profile.ApiKey = request.ApiKey.Trim();
        profile.LoginPath = NormalizeOptional(request.LoginPath);
        profile.LoginHttpMethod = request.LoginHttpMethod;
        profile.LoginBodyTemplate = request.LoginBodyTemplate;
        profile.DefaultHeadersJson = NormalizeOptional(request.DefaultHeadersJson);
        profile.SessionIdJsonPath = NormalizeOptional(request.SessionIdJsonPath);
        profile.BaseUrl = normalizedBaseUrl;
        profile.Language = request.Language.Trim();
        profile.DisconnectSameUser = request.DisconnectSameUser;
        profile.FirmaKodu = request.FirmaKodu;
        profile.DonemKodu = request.DonemKodu;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(profile);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await dbContext.ApiProfiles
            .Where(profile => profile.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? NotFound() : NoContent();
    }

    private static string NormalizeBaseUrl(string? value)
    {
        return value?.Trim().TrimEnd('/') ?? string.Empty;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ApiProfileResponse ToResponse(ApiProfile profile)
    {
        return new ApiProfileResponse(
            profile.Id,
            profile.Name,
            profile.BaseUrl,
            profile.LoginPath,
            profile.LoginHttpMethod,
            profile.LoginBodyTemplate,
            profile.DefaultHeadersJson,
            profile.SessionIdJsonPath,
            profile.Username,
            profile.Language,
            profile.Password,
            profile.ApiKey,
            profile.FirmaKodu,
            profile.DonemKodu,
            profile.DisconnectSameUser,
            profile.LastLoginAt,
            profile.CreatedAt,
            profile.UpdatedAt);
    }
}
