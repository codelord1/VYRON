using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace Vyron.CustomerApp.Services;

public static class ApiErrorHelper
{
    public const string OfflineMessage = "You're offline. Please check your internet connection and try again.";

    public static bool HasInternetAccess =>
        Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public static string ForStatusCode(HttpStatusCode statusCode, string? responseBody = null)
    {
        LogDebug($"HTTP {(int)statusCode}: {responseBody}");

        if (statusCode == HttpStatusCode.BadRequest)
        {
            var serverMessage = ExtractMessage(responseBody);
            var duplicate = responseBody?.Contains("exists", StringComparison.OrdinalIgnoreCase) == true
                || responseBody?.Contains("already", StringComparison.OrdinalIgnoreCase) == true
                || responseBody?.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true;

            if (duplicate)
                return "This phone number already exists. Please login instead.";

            return IsSafeUserMessage(serverMessage)
                ? serverMessage!
                : "Something looks incorrect. Please check your details and try again.";
        }

        return statusCode switch
        {
            HttpStatusCode.Unauthorized => "SESSION_EXPIRED",
            HttpStatusCode.Forbidden => "You don't have permission to perform this action.",
            HttpStatusCode.NotFound => "We couldn't find what you're looking for.",
            HttpStatusCode.RequestTimeout => "The request took too long. Please try again.",
            >= HttpStatusCode.InternalServerError => "Something went wrong on our side. Please try again shortly.",
            _ => "Something went wrong. Please try again."
        };
    }

    public static string ForException(Exception ex)
    {
        LogDebug($"{ex.GetType().Name}: {ex}");

        if (ex.Message.Contains("WebSocket", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
        {
            return "We couldn't connect to Vyron right now. Please check your connection and try again.";
        }

        return ex switch
        {
            TaskCanceledException => "The request took too long. Please try again.",
            HttpRequestException => HasInternetAccess
                ? "We couldn't reach VYRON right now. Please try again."
                : OfflineMessage,
            _ => "Something went wrong. Please try again."
        };
    }

    public static string FriendlyContextMessage(string? error, string context)
    {
        if (string.IsNullOrWhiteSpace(error))
            return context switch
            {
                "stores" => "We couldn't load stores right now. Please try again.",
                "orders" => "We couldn't load your orders right now. Please try again.",
                "payment" => "We couldn't confirm this payment right now. Please try again.",
                _ => "Something went wrong. Please try again."
            };

        if (error == "SESSION_EXPIRED" || error == OfflineMessage)
            return error;

        return context switch
        {
            "stores" when IsGeneric(error) => "We couldn't load stores right now. Please try again.",
            "orders" when IsGeneric(error) => "We couldn't load your orders right now. Please try again.",
            "payment" when IsGeneric(error) => "We couldn't confirm this payment right now. Please try again.",
            _ => error
        };
    }

    private static bool IsGeneric(string error) =>
        error.Contains("Something went wrong", StringComparison.OrdinalIgnoreCase)
        || error.Contains("couldn't reach", StringComparison.OrdinalIgnoreCase)
        || error.Contains("request took too long", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractMessage(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out var error))
                return error.GetString();

            if (root.TryGetProperty("Error", out var pascalError))
                return pascalError.GetString();

            if (root.TryGetProperty("message", out var message))
                return message.GetString();

            if (root.TryGetProperty("Message", out var pascalMessage))
                return pascalMessage.GetString();
        }
        catch (JsonException)
        {
            return responseBody.Length <= 160 ? responseBody : null;
        }

        return null;
    }

    private static bool IsSafeUserMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return !message.Contains("Exception", StringComparison.OrdinalIgnoreCase)
            && !message.Contains("StackTrace", StringComparison.OrdinalIgnoreCase)
            && !message.Contains(" at ", StringComparison.OrdinalIgnoreCase)
            && !message.Contains("System.", StringComparison.OrdinalIgnoreCase);
    }

    [Conditional("DEBUG")]
    private static void LogDebug(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Debug.WriteLine(message);
    }
}
