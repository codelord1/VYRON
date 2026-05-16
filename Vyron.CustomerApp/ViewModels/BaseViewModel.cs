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

    protected void SetError(string? message) => ErrorMessage = message;
    protected void ClearMessages() { ErrorMessage = null; SuccessMessage = null; }

    protected static async Task HandleUnauthorized()
    {
        Models.AppSession.Current.Clear();
        await Shell.Current.GoToAsync("//login", animate: false);
    }

    protected async Task<(T? result, bool handled)> SafeCallAsync<T>(
        Func<Task<(T? Data, string? Error)>> call)
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
                ErrorMessage = error;
                return (default, false);
            }
            return (data, false);
        }
        finally { IsBusy = false; }
    }
}
