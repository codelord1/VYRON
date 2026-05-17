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
        // Returning users with a valid token go straight to Home.
        // Everyone else lands on the login screen.
        var restored = await _auth.TryRestoreSessionAsync();
        if (restored)
        {
            await Shell.Current.GoToAsync(AppRoutes.Home, animate: false);
            return;
        }

        var onboardingDone = Preferences.Default.Get("Vyron.Customer.HasCompletedOnboarding", false);
        if (!onboardingDone)
        {
            await Shell.Current.GoToAsync(AppRoutes.Onboarding, animate: false);
            return;
        }

        var pickupDone = Preferences.Default.Get("Vyron.Customer.HasCompletedPickupLocationSetup", false);
        await Shell.Current.GoToAsync(pickupDone ? AppRoutes.Login : AppRoutes.PickupLocation, animate: false);
    }
}
