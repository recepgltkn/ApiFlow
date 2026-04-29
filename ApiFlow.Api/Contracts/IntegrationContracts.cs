using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ApiFlow.Api.Contracts;

public sealed record ApiLoginResponse(string? SessionId, JsonElement Raw);

public sealed record ApiExecutionRequest(
    JsonElement? Filters = null,
    JsonElement? Sorts = null,
    JsonElement? Params = null,
    JsonElement? Body = null,
    int Limit = 15000,
    int Offset = 0,
    bool ForceLogin = false,
    bool SaveToSqlServer = false,
    int? SqlProfileId = null);

public sealed record ApiExecutionResponse(JsonElement Data, int SavedRows);

public sealed record ApiOperationInfo(
    int ProfileId,
    string Key,
    string HttpMethod,
    string Path,
    string? RequestBodyTemplate = null,
    string? HeadersJson = null,
    string? ResultJsonPath = null,
    string? TargetTableName = null,
    bool CreateTableIfMissing = true,
    bool AddMissingColumns = true,
    bool ClearTableBeforeImport = false);

public sealed record ApiEndpointRequest(
    int ProfileId,
    [Required, MaxLength(100)] string Key,
    [Required, MaxLength(20)] string HttpMethod,
    [Required, MaxLength(300)] string Path,
    [MaxLength(2000)] string? RequestBodyTemplate = null,
    [MaxLength(4000)] string? HeadersJson = null,
    [MaxLength(100)] string? ResultJsonPath = null,
    [MaxLength(128)] string? TargetTableName = null,
    bool CreateTableIfMissing = true,
    bool AddMissingColumns = true,
    bool ClearTableBeforeImport = false);
