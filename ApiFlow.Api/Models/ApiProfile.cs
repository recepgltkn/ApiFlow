using System.ComponentModel.DataAnnotations;

namespace ApiFlow.Api.Models;

public sealed class ApiProfile
{
    public int Id { get; set; }

    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(300)]
    public required string BaseUrl { get; set; }

    [MaxLength(300)]
    public string? LoginPath { get; set; }

    [MaxLength(20)]
    public string LoginHttpMethod { get; set; } = "POST";

    [MaxLength(2000)]
    public string? LoginBodyTemplate { get; set; }

    [MaxLength(100)]
    public required string Username { get; set; }

    [MaxLength(300)]
    public required string Password { get; set; }

    [MaxLength(20)]
    public string Language { get; set; } = "tr";

    [MaxLength(300)]
    public required string ApiKey { get; set; }

    public bool DisconnectSameUser { get; set; } = true;

    public int FirmaKodu { get; set; }

    public int DonemKodu { get; set; }

    [MaxLength(300)]
    public string? LastSessionId { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
