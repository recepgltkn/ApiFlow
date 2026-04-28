using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ApiFlow.Api.Contracts;
using ApiFlow.Api.Models;

namespace ApiFlow.Api.Services;

public sealed class ExternalApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ApiLoginResponse> LoginAsync(ApiProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.LoginPath))
        {
            throw new InvalidOperationException("Profil için login endpointi yapılandırılmamış.");
        }

        var body = string.IsNullOrWhiteSpace(profile.LoginBodyTemplate)
            ? JsonSerializer.Serialize(new
            {
                username = profile.Username,
                password = profile.Password,
                apiKey = profile.ApiKey,
                language = profile.Language,
                firmaKodu = profile.FirmaKodu,
                donemKodu = profile.DonemKodu
            }, JsonOptions)
            : ApplyTemplate(profile.LoginBodyTemplate, profile, null, null);

        using var message = new HttpRequestMessage(
            CreateMethod(profile.LoginHttpMethod),
            BuildUri(profile, profile.LoginPath));
        AddJsonBody(message, body);
        AddHeaders(message, profile.DefaultHeadersJson, profile, null);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        using var document = await ReadApiResponseAsync(response, cancellationToken);
        var root = document.RootElement.Clone();
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("code", out var code) &&
            code.GetString() is { } codeValue &&
            codeValue != "200")
        {
            var messageText = TryReadString(root, "msg") ?? root.GetRawText();
            throw new InvalidOperationException($"Login başarısız oldu. Kod: {codeValue}, Mesaj: {messageText}");
        }

        var sessionId = TryReadString(root, profile.SessionIdJsonPath)
            ?? TryReadString(root, "access_token")
            ?? TryReadString(root, "token")
            ?? TryReadString(root, "session_id")
            ?? TryReadString(root, "msg");

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new InvalidOperationException("Login cevabında session/token bulunamadı.");
        }

        return new ApiLoginResponse(sessionId, root);
    }

    public async Task<JsonElement> ExecuteAsync(
        ApiProfile profile,
        ApiOperationInfo operation,
        ApiExecutionRequest request,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        var body = request.Body?.GetRawText()
            ?? (!string.IsNullOrWhiteSpace(operation.RequestBodyTemplate)
                ? ApplyTemplate(operation.RequestBodyTemplate, profile, request, sessionId)
                : JsonSerializer.Serialize(new
                {
                    sessionId,
                    username = profile.Username,
                    apiKey = profile.ApiKey,
                    firmaKodu = profile.FirmaKodu,
                    donemKodu = profile.DonemKodu,
                    filters = request.Filters,
                    sorts = request.Sorts,
                    @params = request.Params,
                    limit = request.Limit,
                    offset = request.Offset
                }, JsonOptions));

        using var message = new HttpRequestMessage(
            CreateMethod(operation.HttpMethod),
            BuildUri(profile, operation.Path));

        if (!HttpMethods.Get.Equals(operation.HttpMethod, StringComparison.OrdinalIgnoreCase))
        {
            AddJsonBody(message, body);
        }

        AddHeaders(message, profile.DefaultHeadersJson, profile, sessionId);
        AddHeaders(message, operation.HeadersJson, profile, sessionId);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        using var document = await ReadApiResponseAsync(response, cancellationToken);
        return document.RootElement.Clone();
    }

    private static Uri BuildUri(ApiProfile profile, string path)
    {
        var baseUri = new Uri(profile.BaseUrl.TrimEnd('/') + "/");
        return new Uri(baseUri, path.TrimStart('/'));
    }

    private static HttpMethod CreateMethod(string method)
    {
        return new HttpMethod(string.IsNullOrWhiteSpace(method) ? "POST" : method.Trim().ToUpperInvariant());
    }

    private static void AddJsonBody(HttpRequestMessage message, string body)
    {
        message.Content = new StringContent(body, Encoding.UTF8);
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    }

    private static void AddHeaders(HttpRequestMessage message, string? headersJson, ApiProfile profile, string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
        {
            return;
        }

        using var document = JsonDocument.Parse(ApplyTemplate(headersJson, profile, null, sessionId));
        foreach (var header in document.RootElement.EnumerateObject())
        {
            var value = header.Value.ValueKind == JsonValueKind.String
                ? header.Value.GetString()
                : header.Value.GetRawText();

            if (!string.IsNullOrWhiteSpace(value))
            {
                message.Headers.TryAddWithoutValidation(header.Name, value);
            }
        }
    }

    private static string ApplyTemplate(string template, ApiProfile? profile, ApiExecutionRequest? request, string? sessionId)
    {
        var result = template
            .Replace("{{sessionId}}", sessionId ?? string.Empty)
            .Replace("{{limit}}", request?.Limit.ToString() ?? string.Empty)
            .Replace("{{offset}}", request?.Offset.ToString() ?? string.Empty);

        if (profile is not null)
        {
            result = result
                .Replace("{{username}}", profile.Username)
                .Replace("{{password}}", profile.Password)
                .Replace("{{apikey}}", profile.ApiKey)
                .Replace("{{apiKey}}", profile.ApiKey)
                .Replace("{{language}}", profile.Language)
                .Replace("{{firmaKodu}}", profile.FirmaKodu.ToString())
                .Replace("{{donemKodu}}", profile.DonemKodu.ToString());
        }

        if (request is not null)
        {
            result = result
                .Replace("{{filters}}", request.Filters?.GetRawText() ?? "null")
                .Replace("{{sorts}}", request.Sorts?.GetRawText() ?? "null")
                .Replace("{{params}}", request.Params?.GetRawText() ?? "null");
        }

        return result;
    }

    private static string? TryReadString(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static async Task<JsonDocument> ReadApiResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(string.IsNullOrWhiteSpace(content) ? "{}" : content);
        }
        catch (JsonException)
        {
            throw new HttpRequestException(
                $"API geçerli JSON dönmedi. HTTP {(int)response.StatusCode} {(response.RequestMessage?.RequestUri ?? new Uri("about:blank"))}: {content}");
        }

        if (response.IsSuccessStatusCode)
        {
            return document;
        }

        using (document)
        {
            throw new HttpRequestException(
                $"API isteği başarısız oldu. HTTP {(int)response.StatusCode} {(response.RequestMessage?.RequestUri ?? new Uri("about:blank"))}: {document.RootElement}");
        }
    }
}
