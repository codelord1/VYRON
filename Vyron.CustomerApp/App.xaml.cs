using Vyron.CustomerApp.Services;

namespace Vyron.CustomerApp;

public partial class App : Application
{
    private readonly IAuthService _auth;

    public App(IAuthService auth, AppShell shell)
    {
        InitializeComponent();
        _auth = auth;
        MainPage = shell;
    }

    protected override async void OnStart()
    {
        base.OnStart();
        // Try to restore a saved session (SecureStorage JWT + refresh).
        // Returning users with a valid token go straight to Home/Stores.
        // Everyone else lands on the login screen.
        var restored = await _auth.TryRestoreSessionAsync();
        // Navigate to the Stores tab root (not just //main) so tab state is always clean
        await Shell.Current.GoToAsync(restored ? AppRoutes.Stores : AppRoutes.Login, animate: false);
    }
}
