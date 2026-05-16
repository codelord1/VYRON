using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vyron.CustomerApp.Models;

namespace Vyron.CustomerApp.Services;

/// <summary>
/// Core HTTP wrapper. Handles Bearer token injection, 401 handling, and JSON serialization.
/// All methods return (T? result, string? errorMessage).
/// </summary>
public class ApiService
{
    private readonly IHttpClientFactory _factory;

    private static readonly JsonSerializerOptions _json = new()
    {
        // CamelCase matches ASP.NET Core's default serialization policy so the
        // request body keys ("phone", "code" …) align with what the server expects.
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,   // also used for response deserialization
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
        try
        {
            using var client = CreateClient();
            var response = await client.GetAsync(path);
            return await ParseResponse<T>(response);
        }
        catch (Exception ex) { return (default, ToMessage(ex)); }
    }

    public async Task<(T? Data, string? Error)> PostAsync<T>(string path, object? body = null)
    {
        try
        {
            using var client = CreateClient();
            var content = body != null
                ? new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json")
                : null;
            var response = await client.PostAsync(path, content);
            return await ParseResponse<T>(response);
        }
        catch (Exception ex) { return (default, ToMessage(ex)); }
    }

    public async Task<(T? Data, string? Error)> PutAsync<T>(string path, object? body = null)
    {
        try
        {
            using var client = CreateClient();
            var content = body != null
                ? new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json")
                : null;
            var response = await client.PutAsync(path, content);
            return await ParseResponse<T>(response);
        }
        catch (Exception ex) { return (default, ToMessage(ex)); }
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
            await SecureStorage.Default.SetAsync("refresh_token", "");
            return (default, "SESSION_EXPIRED");
        }

        // Extract message from JSON error response (case-insensitive property search)
        string message = ExtractErrorMessage(body, (int)response.StatusCode);
        return (default, message);
    }

    /// <summary>
    /// Tries to extract a human-readable error string from an API error response body.
    /// Handles JSON (ASP.NET Core ProblemDetails / our SendOtpResponse) and non-JSON
    /// bodies (IIS Express plain-text / HTML 400s).
    /// </summary>
    private static string ExtractErrorMessage(string body, int statusCode)
    {
        var fallback = $"Error {statusCode}";

        if (string.IsNullOrWhiteSpace(body))
        {
#if DEBUG
            // Empty body on a 400 from IIS Express almost always means the Host
            // header was rejected.  Guide the developer immediately.
            if (statusCode == 400)
                return $"400 Bad Request with empty body.\n\n" +
                       $"This is usually IIS Express rejecting the Android emulator's Host header.\n" +
                       $"FIX: In Visual Studio change the run profile from 'IIS Express' to 'Vyron.API'\n" +
                       $"     so Kestrel handles the request (Kestrel has no host-header restriction).\n" +
                       $"API should then be on http://localhost:50680  →  CustomerApp: {AppConstants.ApiBaseUrl}";
#endif
            return fallback;
        }

        try
        {
            // Use case-insensitive parsing so "message"/"Message" both work
            // regardless of ASP.NET Core's serialisation policy.
            using var doc = JsonDocument.Parse(body,
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            var root = doc.RootElement;

            // 1. Our custom SendOtpResponse / generic API error  →  "message"
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name.Equals("message", StringComparison.OrdinalIgnoreCase))
                    return prop.Value.GetString() ?? fallback;
            }

            // 2. ASP.NET Core ProblemDetails  →  "title"
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name.Equals("title", StringComparison.OrdinalIgnoreCase))
                    return prop.Value.GetString() ?? fallback;
            }
        }
        catch
        {
            // Body is HTML or plain text (e.g. IIS Express default error page)
#if DEBUG
            if (statusCode == 400)
                return $"400 Bad Request (non-JSON body — likely IIS Express Host-header rejection).\n\n" +
                       $"FIX: Switch Visual Studio run profile from 'IIS Express' to 'Vyron.API'.\n" +
                       $"Kestrel (port 50680 HTTP) does not restrict Host headers.\n" +
                       $"Current CustomerApp base URL: {AppConstants.ApiBaseUrl}";
#endif
        }

        return fallback;
    }

    private static string ToMessage(Exception ex) => ex switch
    {
        TaskCanceledException =>
            $"Could not reach VYRON API (request timed out).\n\n" +
            $"Check:\n" +
            $"• Vyron.API is running (Swagger: https://localhost:44344/swagger)\n" +
            $"• AppConstants.ApiBaseUrl is correct\n" +
            $"• Current URL: {AppConstants.ApiBaseUrl}",
        HttpRequestException hre when hre.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase)
                                   || hre.Message.Contains("SSL",         StringComparison.OrdinalIgnoreCase)
                                   || hre.Message.Contains("trust",       StringComparison.OrdinalIgnoreCase) =>
            $"HTTPS certificate error connecting to {AppConstants.ApiBaseUrl}\n\n" +
            $"This usually means the app is running in Release mode (cert bypass is DEBUG-only),\n" +
            $"or the Visual Studio dev certificate hasn't been trusted on this machine.\n" +
            $"Run: dotnet dev-certs https --trust",
        HttpRequestException hre =>
            $"Cannot reach VYRON API: {hre.Message}\n\n" +
            $"Check:\n" +
            $"• Vyron.API is running on {AppConstants.ApiBaseUrl}\n" +
            $"• Correct port (launchSettings.json → applicationUrl)\n" +
            $"• Android emulator uses 10.0.2.2, not localhost",
        _ =>
#if DEBUG
            $"[DEBUG] {ex.GetType().Name}: {ex.Message}",
#else
            "An unexpected error occurred. Please try again.",
#endif
    };
}
