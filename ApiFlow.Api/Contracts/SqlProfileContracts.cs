using System.ComponentModel.DataAnnotations;

namespace ApiFlow.Api.Contracts;

public sealed record SqlProfileRequest(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(100)] string Key,
    [Required, MaxLength(300)] string Host,
    int? Port,
    [MaxLength(100)] string? InstanceName,
    [Required, MaxLength(200)] string DatabaseName,
    [Required, MaxLength(200)] string Username,
    [Required, MaxLength(300)] string Password,
    [MaxLength(200)] string? ApplicationName,
    bool TrustServerCertificate,
    bool Encrypt,
    [Required, MaxLength(128)] string SchemaName);

public sealed record SqlProfileResponse(
    int Id,
    string Name,
    string Key,
    string Host,
    int? Port,
    string? InstanceName,
    string DatabaseName,
    string Username,
    string Password,
    string? ApplicationName,
    bool TrustServerCertificate,
    bool Encrypt,
    string SchemaName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
