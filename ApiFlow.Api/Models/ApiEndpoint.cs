using System.ComponentModel.DataAnnotations;

namespace ApiFlow.Api.Models;

public sealed class ApiEndpoint
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public required string Key { get; set; }

    [Required, MaxLength(20)]
    public string HttpMethod { get; set; } = "POST";

    [MaxLength(2000)]
    public string? RequestBodyTemplate { get; set; }

    [Required, MaxLength(300)]
    public required string Path { get; set; }
}
