using System.ComponentModel.DataAnnotations;

namespace ApiFlow.Api.Contracts;

public sealed record ApiProfileRequest(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(100)] string Username,
    [Required, MaxLength(300)] string Password,
    [Required, MaxLength(300)] string ApiKey,
    [Required, MaxLength(300)] string BaseUrl,
    int FirmaKodu,
    int DonemKodu,
    [MaxLength(300)] string? LoginPath = null,
    [MaxLength(20)] string LoginHttpMethod = "POST",
    [MaxLength(2000)] string? LoginBodyTemplate = null,
    [MaxLength(4000)] string? DefaultHeadersJson = null,
    [MaxLength(100)] string? SessionIdJsonPath = null,
    [MaxLength(20)] string Language = "tr",
    bool DisconnectSameUser = true);

public sealed record ApiProfileUpdateRequest(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(100)] string Username,
    [Required, MaxLength(300)] string Password,
    [Required, MaxLength(300)] string ApiKey,
    [Required, MaxLength(300)] string BaseUrl,
    int FirmaKodu,
    int DonemKodu,
    [MaxLength(300)] string? LoginPath = null,
    [MaxLength(20)] string LoginHttpMethod = "POST",
    [MaxLength(2000)] string? LoginBodyTemplate = null,
    [MaxLength(4000)] string? DefaultHeadersJson = null,
    [MaxLength(100)] string? SessionIdJsonPath = null,
    [MaxLength(20)] string Language = "tr",
    bool DisconnectSameUser = true);

public sealed record ApiProfileResponse(
    int Id,
    string Name,
    string BaseUrl,
    string? LoginPath,
    string LoginHttpMethod,
    string? LoginBodyTemplate,
    string? DefaultHeadersJson,
    string? SessionIdJsonPath,
    string Username,
    string Language,
    string Password,
    string ApiKey,
    int FirmaKodu,
    int DonemKodu,
    bool DisconnectSameUser,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
