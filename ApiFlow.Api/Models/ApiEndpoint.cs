using System.ComponentModel.DataAnnotations;

namespace ApiFlow.Api.Models;

public sealed class ApiEndpoint
{
    public int Id { get; set; }

    public int ProfileId { get; set; }

    public ApiProfile? Profile { get; set; }

    [Required, MaxLength(100)]
    public required string Key { get; set; }

    [Required, MaxLength(20)]
    public string HttpMethod { get; set; } = "POST";

    [MaxLength(2000)]
    public string? RequestBodyTemplate { get; set; }

    [MaxLength(4000)]
    public string? HeadersJson { get; set; }

    [MaxLength(100)]
    public string? ResultJsonPath { get; set; }

    [MaxLength(128)]
    public string? TargetTableName { get; set; }

    public bool CreateTableIfMissing { get; set; } = true;

    public bool AddMissingColumns { get; set; } = true;

    public bool ClearTableBeforeImport { get; set; }

    [Required, MaxLength(300)]
    public required string Path { get; set; }
}
