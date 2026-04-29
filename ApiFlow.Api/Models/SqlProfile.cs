using System.ComponentModel.DataAnnotations;

namespace ApiFlow.Api.Models;

public sealed class SqlProfile
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public required string Name { get; set; }

    [Required, MaxLength(100)]
    public required string Key { get; set; }

    [Required, MaxLength(300)]
    public required string Host { get; set; }

    public int? Port { get; set; }

    [MaxLength(100)]
    public string? InstanceName { get; set; }

    [Required, MaxLength(200)]
    public required string DatabaseName { get; set; }

    [Required, MaxLength(200)]
    public required string Username { get; set; }

    [Required, MaxLength(300)]
    public required string Password { get; set; }

    [MaxLength(200)]
    public string? ApplicationName { get; set; }

    public bool TrustServerCertificate { get; set; } = true;

    public bool Encrypt { get; set; }

    [Required, MaxLength(128)]
    public string SchemaName { get; set; } = "dbo";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
