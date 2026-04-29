using ApiFlow.Api.Contracts;
using ApiFlow.Api.Data;
using ApiFlow.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiFlow.Api.Controllers;

[ApiController]
[Route("api/sql-profiles")]
public sealed class SqlProfilesController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SqlProfileResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var profiles = await dbContext.SqlProfiles
            .AsNoTracking()
            .OrderBy(profile => profile.Name)
            .Select(profile => ToResponse(profile))
            .ToListAsync(cancellationToken);

        return profiles;
    }

    [HttpPost]
    public async Task<ActionResult<SqlProfileResponse>> Create(SqlProfileRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await dbContext.SqlProfiles.AnyAsync(profile => profile.Name == name, cancellationToken))
        {
            return Conflict(new { message = $"'{name}' adında bir SQL profili zaten var." });
        }

        var profile = new SqlProfile
        {
            Name = name,
            Key = request.Key.Trim(),
            Host = request.Host.Trim(),
            Port = request.Port,
            InstanceName = NormalizeOptional(request.InstanceName),
            DatabaseName = request.DatabaseName.Trim(),
            Username = request.Username.Trim(),
            Password = request.Password,
            ApplicationName = NormalizeOptional(request.ApplicationName),
            TrustServerCertificate = request.TrustServerCertificate,
            Encrypt = request.Encrypt,
            SchemaName = NormalizeIdentifier(request.SchemaName, "dbo")
        };

        dbContext.SqlProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetAll), ToResponse(profile));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SqlProfileResponse>> Update(int id, SqlProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await dbContext.SqlProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();
        if (await dbContext.SqlProfiles.AnyAsync(candidate => candidate.Id != id && candidate.Name == name, cancellationToken))
        {
            return Conflict(new { message = $"'{name}' adında bir SQL profili zaten var." });
        }

        profile.Name = name;
        profile.Key = request.Key.Trim();
        profile.Host = request.Host.Trim();
        profile.Port = request.Port;
        profile.InstanceName = NormalizeOptional(request.InstanceName);
        profile.DatabaseName = request.DatabaseName.Trim();
        profile.Username = request.Username.Trim();
        profile.Password = request.Password;
        profile.ApplicationName = NormalizeOptional(request.ApplicationName);
        profile.TrustServerCertificate = request.TrustServerCertificate;
        profile.Encrypt = request.Encrypt;
        profile.SchemaName = NormalizeIdentifier(request.SchemaName, "dbo");
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(profile);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await dbContext.SqlProfiles
            .Where(profile => profile.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? NotFound() : NoContent();
    }

    private static SqlProfileResponse ToResponse(SqlProfile profile)
    {
        return new SqlProfileResponse(
            profile.Id,
            profile.Name,
            profile.Key,
            profile.Host,
            profile.Port,
            profile.InstanceName,
            profile.DatabaseName,
            profile.Username,
            profile.Password,
            profile.ApplicationName,
            profile.TrustServerCertificate,
            profile.Encrypt,
            profile.SchemaName,
            profile.CreatedAt,
            profile.UpdatedAt);
    }

    private static string NormalizeIdentifier(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
