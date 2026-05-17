namespace Vyron.CustomerApp.Views;

public partial class PickupLocationPage : ContentPage
{
    public PickupLocationPage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.Onboarding, animate: true);
    }

    private async void OnContinueClicked(object sender, EventArgs e)
    {
        Preferences.Default.Set("Vyron.Customer.HasCompletedPickupLocationSetup", true);
        Preferences.Default.Set("Vyron.Customer.PickupLocation", "Lekki Phase 1");
        await Shell.Current.GoToAsync(AppRoutes.Login, animate: true);
    }
}
