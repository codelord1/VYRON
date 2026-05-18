using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vyron.CustomerApp.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    public bool IsNotBusy => !IsBusy;

    protected void SetError(string? message) => ErrorMessage = FriendlyError(message);
    protected void ClearMessages() { ErrorMessage = null; SuccessMessage = null; }

    protected static async Task HandleUnauthorized()
    {
        Models.AppSession.Current.Clear();
        await Shell.Current.GoToAsync(AppRoutes.Login, animate: false);
    }

    protected async Task<(T? result, bool handled)> SafeCallAsync<T>(
        Func<Task<(T? Data, string? Error)>> call,
        string? errorContext = null)
    {
        ClearMessages();
        IsBusy = true;
        try
        {
            var (data, error) = await call();
            if (error == "SESSION_EXPIRED")
            {
                await HandleUnauthorized();
                return (default, true);
            }
            if (error != null)
            {
                ErrorMessage = FriendlyError(errorContext == null
                    ? error
                    : Services.ApiErrorHelper.FriendlyContextMessage(error, errorContext));
                return (default, false);
            }
            return (data, false);
        }
        finally { IsBusy = false; }
    }

    private static string? FriendlyError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        if (message == "SESSION_EXPIRED")
            return "Your session has expired. Please login again.";

        if (message.Contains("WebSocket", StringComparison.OrdinalIgnoreCase)
            || message.Contains("connection", StringComparison.OrdinalIgnoreCase))
            return "We couldn't connect to Vyron right now. Please check your connection and try again.";

        if (message.Contains("Exception", StringComparison.OrdinalIgnoreCase)
            || message.Contains("stack", StringComparison.OrdinalIgnoreCase))
            return "Something went wrong. Please try again.";

        if (message.Contains("Error 400", StringComparison.OrdinalIgnoreCase)
            || message.Contains("400 Bad Request", StringComparison.OrdinalIgnoreCase))
            return "Something looks incorrect. Please check your details and try again.";

        if (message.Contains("Error 500", StringComparison.OrdinalIgnoreCase)
            || message.Contains("500", StringComparison.OrdinalIgnoreCase))
            return "Something went wrong on our side. Please try again shortly.";

        return message;
    }
}
