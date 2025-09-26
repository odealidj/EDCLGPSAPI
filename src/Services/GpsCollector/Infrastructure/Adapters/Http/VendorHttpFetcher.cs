using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace GpsCollector.Infrastructure.Adapters.Http;

// Client HTTP pemanggil vendor dengan caching Bearer token & retry Polly v8
public sealed class VendorHttpFetcher : IVendorHttpFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VendorHttpFetcher> _logger;
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePipeline;
    private readonly ConcurrentDictionary<Guid, CachedAuthToken> _tokenCache = new();

    private sealed record CachedAuthToken(string Token, DateTime ExpiresAt);

    public VendorHttpFetcher(IHttpClientFactory httpClientFactory, ILogger<VendorHttpFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        // Polly v8 Resilience Pipeline: 3 retry dengan exponential backoff & jitter
        _resiliencePipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .HandleResult(resp => (int)resp.StatusCode >= 500 || resp.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            })
            .AddTimeout(TimeSpan.FromSeconds(25))
            .Build();
    }

    public async Task<VendorFetchResult> FetchEndpointAsync(VendorConfig vendor, VendorEndpointConfig endpoint, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("VendorHttpClient");
        var requestUri = BuildRequestUri(endpoint);

        var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), requestUri);

        // Pasang Request Body jika ada
        if (!string.IsNullOrWhiteSpace(endpoint.BodiesJson) && endpoint.BodiesJson != "{}")
        {
            request.Content = new StringContent(endpoint.BodiesJson, Encoding.UTF8, "application/json");
        }

        // Pasang Headers
        ApplyHeaders(request, endpoint.HeadersJson);

        // Pasang Authorization
        await ApplyAuthorizationAsync(client, request, vendor, ct);

        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(
                async token => await client.SendAsync(request, token),
                ct);

            var body = await response.Content.ReadAsStringAsync(ct);

            return new VendorFetchResult(
                IsSuccess: response.IsSuccessStatusCode,
                StatusCode: (int)response.StatusCode,
                ReasonPhrase: response.ReasonPhrase,
                ResponseBody: body,
                RequestUrl: requestUri.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP call failed for vendor {VendorName} endpoint {Url}: {Message}", vendor.VendorName, endpoint.BaseUrl, ex.Message);
            return new VendorFetchResult(
                IsSuccess: false,
                StatusCode: 500,
                ReasonPhrase: ex.Message,
                ResponseBody: null,
                RequestUrl: requestUri.ToString());
        }
    }

    private static Uri BuildRequestUri(VendorEndpointConfig endpoint)
    {
        var uriBuilder = new UriBuilder(endpoint.BaseUrl);
        var queryParams = new List<string>();

        // Parse query string yang sudah ada di URL
        if (!string.IsNullOrWhiteSpace(uriBuilder.Query))
        {
            var existing = uriBuilder.Query.TrimStart('?');
            if (!string.IsNullOrWhiteSpace(existing))
            {
                queryParams.Add(existing);
            }
        }

        // Tambah Params dari JSON
        AppendJsonParamsToQuery(endpoint.ParamsJson, queryParams);

        // Tambah VarParams dari JSON (misal last_id)
        AppendJsonParamsToQuery(endpoint.VarParamsJson, queryParams);

        if (queryParams.Count > 0)
        {
            uriBuilder.Query = string.Join("&", queryParams);
        }

        return uriBuilder.Uri;
    }

    private static void AppendJsonParamsToQuery(string? json, List<string> queryParams)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var val = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Number => prop.Value.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => prop.Value.GetRawText()
                    };
                    if (val != null)
                    {
                        queryParams.Add($"{Uri.EscapeDataString(prop.Name)}={Uri.EscapeDataString(val)}");
                    }
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in item.EnumerateObject())
                        {
                            var val = prop.Value.GetRawText().Trim('"');
                            queryParams.Add($"{Uri.EscapeDataString(prop.Name)}={Uri.EscapeDataString(val)}");
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Abaikan jika json invalid
        }
    }

    private static void ApplyHeaders(HttpRequestMessage request, string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson)) return;

        try
        {
            using var doc = JsonDocument.Parse(headersJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var val = prop.Value.GetString();
                    if (!string.IsNullOrEmpty(val))
                    {
                        request.Headers.TryAddWithoutValidation(prop.Name, val);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Abaikan jika json invalid
        }
    }

    private async Task ApplyAuthorizationAsync(HttpClient client, HttpRequestMessage request, VendorConfig vendor, CancellationToken ct)
    {
        if (!vendor.RequiredAuth) return;

        if (string.Equals(vendor.AuthType, "Basic", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(vendor.Username))
            {
                var credentials = $"{vendor.Username}:{vendor.Password}";
                var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64);
            }
        }
        else if (string.Equals(vendor.AuthType, "Bearer", StringComparison.OrdinalIgnoreCase) && vendor.Auth != null)
        {
            var token = await GetOrFetchBearerTokenAsync(vendor.Auth, ct);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
    }

    private async Task<string?> GetOrFetchBearerTokenAsync(VendorAuthConfig auth, CancellationToken ct)
    {
        // Cek cache in-memory
        if (_tokenCache.TryGetValue(auth.GpsVendorId, out var cached) && cached.ExpiresAt > DateTime.UtcNow.AddMinutes(2))
        {
            return cached.Token;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("VendorAuthClient");
            var authRequest = new HttpRequestMessage(new HttpMethod(auth.Method), auth.BaseUrl);

            if (string.Equals(auth.AuthType, "Basic", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(auth.Username))
            {
                var creds = $"{auth.Username}:{auth.Password}";
                authRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(creds)));
            }

            if (!string.IsNullOrWhiteSpace(auth.BodiesJson))
            {
                if (string.Equals(auth.ContentType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
                {
                    var formData = ParseJsonToFormData(auth.BodiesJson);
                    authRequest.Content = new FormUrlEncodedContent(formData);
                }
                else
                {
                    authRequest.Content = new StringContent(auth.BodiesJson, Encoding.UTF8, "application/json");
                }
            }

            ApplyHeaders(authRequest, auth.HeadersJson);

            var response = await client.SendAsync(authRequest, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to acquire token from {AuthUrl}: {StatusCode} {Reason}", auth.BaseUrl, response.StatusCode, response.ReasonPhrase);
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseBody);

            var token = ExtractTokenFromPath(doc.RootElement, auth.TokenPath ?? "token");
            if (!string.IsNullOrWhiteSpace(token))
            {
                var ttl = TimeSpan.FromMinutes(50);
                _tokenCache[auth.GpsVendorId] = new CachedAuthToken(token, DateTime.UtcNow.Add(ttl));
                return token;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when fetching token for vendor auth {AuthId}: {Message}", auth.Id, ex.Message);
            return null;
        }
    }

    private static List<KeyValuePair<string, string>> ParseJsonToFormData(string json)
    {
        var result = new List<KeyValuePair<string, string>>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    result.Add(new KeyValuePair<string, string>(prop.Name, prop.Value.GetString() ?? prop.Value.GetRawText()));
                }
            }
        }
        catch
        {
            // Abaikan
        }
        return result;
    }

    private static string? ExtractTokenFromPath(JsonElement root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var current = root;
        var parts = path.Split('.');

        foreach (var part in parts)
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var next))
            {
                current = next;
            }
            else
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.GetRawText();
    }
}
