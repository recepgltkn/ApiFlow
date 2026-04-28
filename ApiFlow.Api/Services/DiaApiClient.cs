using System.Net.Http.Json;
using System.Text.Json;
using ApiFlow.Api.Contracts;
using ApiFlow.Api.Models;

namespace ApiFlow.Api.Services;

public sealed class DiaApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DiaLoginResponse> LoginAsync(ApiProfile profile, CancellationToken cancellationToken)
    {
        var request = new
        {
            login = new
            {
                username = profile.Username,
                password = profile.Password,
                disconnect_same_user = profile.DisconnectSameUser.ToString().ToLowerInvariant(),
                language = profile.Language,
                @params = new
                {
                    apikey = profile.ApiKey
                }
            }
        };

        if (string.IsNullOrWhiteSpace(profile.LoginPath))
        {
            throw new InvalidOperationException("Profil için login endpointi yapılandırılmamış.");
        }

        using var response = await httpClient.PostAsJsonAsync(
            BuildUri(profile, profile.LoginPath),
            request,
            JsonOptions,
            cancellationToken);

        var document = await ReadDiaResponseAsync(response, cancellationToken);
        var root = document.RootElement.Clone();

        var sessionId = root.TryGetProperty("msg", out var msg) ? msg.GetString() : null;
        if (root.TryGetProperty("code", out var code) && code.GetString() is { } codeValue && codeValue != "200")
        {
            throw new InvalidOperationException($"DIA login başarısız oldu. Kod: {codeValue}, Mesaj: {sessionId}");
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new InvalidOperationException("DIA login cevabında session id bulunamadı.");
        }

        return new DiaLoginResponse(sessionId, root);
    }

    public async Task<JsonElement> ExecuteListAsync(
        ApiProfile profile,
        DiaOperationInfo operation,
        DiaListRequest request,
        string sessionId,
        CancellationToken cancellationToken)
    {
        object body;

        if (!string.IsNullOrWhiteSpace(operation.RequestBodyTemplate))
        {
            body = BuildRequestBody(operation.RequestBodyTemplate, profile, request, sessionId);
        }
        else
        {
            body = new Dictionary<string, object?>
            {
                [operation.Method] = new Dictionary<string, object?>
                {
                    ["session_id"] = sessionId,
                    ["firma_kodu"] = profile.FirmaKodu,
                    ["donem_kodu"] = profile.DonemKodu,
                    ["filters"] = request.Filters,
                    ["sorts"] = request.Sorts,
                    ["params"] = request.Params,
                    ["limit"] = request.Limit,
                    ["offset"] = request.Offset
                }
            };
        }

        using var response = await httpClient.PostAsJsonAsync(
            BuildUri(profile, operation.Path),
            body,
            JsonOptions,
            cancellationToken);

        using var document = await ReadDiaResponseAsync(response, cancellationToken);
        return document.RootElement.Clone();
    }

    private static Uri BuildUri(ApiProfile profile, string path)
    {
        const string apiV3Prefix = "/api/v3";
        var baseUrl = profile.BaseUrl.TrimEnd('/');

        if (baseUrl.EndsWith(apiV3Prefix, StringComparison.OrdinalIgnoreCase) &&
            path.StartsWith(apiV3Prefix, StringComparison.OrdinalIgnoreCase))
        {
            path = path[apiV3Prefix.Length..];
        }

        var baseUri = new Uri(baseUrl + "/");
        return new Uri(baseUri, path.TrimStart('/'));
    }

    private static object BuildRequestBody(string template, ApiProfile profile, DiaListRequest request, string sessionId)
    {
        return template
            .Replace("{{sessionId}}", sessionId)
            .Replace("{{firmaKodu}}", profile.FirmaKodu.ToString())
            .Replace("{{donemKodu}}", profile.DonemKodu.ToString())
            .Replace("{{limit}}", request.Limit.ToString())
            .Replace("{{offset}}", request.Offset.ToString())
            .Replace("{{username}}", profile.Username)
            .Replace("{{apikey}}", profile.ApiKey);
    }

    private static async Task<JsonDocument> ReadDiaResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            if (response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"DIA geçerli JSON dönmedi. HTTP {(int)response.StatusCode} {(response.RequestMessage?.RequestUri ?? new Uri("about:blank"))}: {content}");
            }

            throw new HttpRequestException(
                $"DIA isteği başarısız oldu. HTTP {(int)response.StatusCode} {(response.RequestMessage?.RequestUri ?? new Uri("about:blank"))}: {content}");
        }

        if (response.IsSuccessStatusCode)
        {
            return document;
        }

        using (document)
        {
            throw new HttpRequestException(
                $"DIA isteği başarısız oldu. HTTP {(int)response.StatusCode} {(response.RequestMessage?.RequestUri ?? new Uri("about:blank"))}: {document.RootElement}");
        }
    }
}
