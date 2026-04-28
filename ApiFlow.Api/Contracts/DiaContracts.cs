using System.Text.Json;

namespace ApiFlow.Api.Contracts;

public sealed record DiaLoginResponse(string SessionId, JsonElement Raw);

public sealed record DiaListRequest(
    JsonElement? Filters = null,
    JsonElement? Sorts = null,
    JsonElement? Params = null,
    int Limit = 15000,
    int Offset = 0,
    bool ForceLogin = false,
    bool SaveToSqlServer = false);

public sealed record DiaOperationResponse(JsonElement Data, int SavedRows);

public sealed record DiaOperationInfo(string Key, string Method, string Path, string? RequestBodyTemplate = null);
