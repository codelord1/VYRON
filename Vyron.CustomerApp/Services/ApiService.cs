using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vyron.CustomerApp.Models;

namespace Vyron.CustomerApp.Services;

/// <summary>
/// Core HTTP wrapper. Handles connectivity checks, Bearer token injection,
/// session expiry, JSON serialization, and friendly user-facing errors.
/// </summary>
public class ApiService
{
    private readonly IHttpClientFactory _factory;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public ApiService(IHttpClientFactory factory) => _factory = factory;

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient("VyronApi");
        var token = AppSession.Current.AccessToken;
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<(T? Data, string? Error)> GetAsync<T>(string path)
    {
        if (!ApiErrorHelper.HasInternetAccess)
            return (default, ApiErrorHelper.OfflineMessage);

        try
        {
            using var client = CreateClient();
            var response = await client.GetAsync(path);
            return await ParseResponse<T>(response);
        }
        catch (Exception ex) { return (default, ApiErrorHelper.ForException(ex)); }
    }

    public async Task<(T? Data, string? Error)> PostAsync<T>(string path, object? body = null)
    {
        if (!ApiErrorHelper.HasInternetAccess)
            return (default, ApiErrorHelper.OfflineMessage);

        try
        {
            using var client = CreateClient();
            var content = body != null
                ? new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json")
                : null;
            var response = await client.PostAsync(path, content);
            return await ParseResponse<T>(response);
        }
        catch (Exception ex) { return (default, ApiErrorHelper.ForException(ex)); }
    }

    public async Task<(T? Data, string? Error)> PutAsync<T>(string path, object? body = null)
    {
        if (!ApiErrorHelper.HasInternetAccess)
            return (default, ApiErrorHelper.OfflineMessage);

        try
        {
            using var client = CreateClient();
            var content = body != null
                ? new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json")
                : null;
            var response = await client.PutAsync(path, content);
            return await ParseResponse<T>(response);
        }
        catch (Exception ex) { return (default, ApiErrorHelper.ForException(ex)); }
    }

    private static async Task<(T? Data, string? Error)> ParseResponse<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            if (string.IsNullOrWhiteSpace(body))
                return (default, null);
            var data = JsonSerializer.Deserialize<T>(body, _json);
            return (data, null);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            AppSession.Current.Clear();
            SecureStorage.Default.Remove("refresh_token");
            return (default, "SESSION_EXPIRED");
        }

        return (default, ApiErrorHelper.ForStatusCode(response.StatusCode, body));
    }
}
