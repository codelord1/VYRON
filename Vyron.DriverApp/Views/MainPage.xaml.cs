namespace Vyron.DriverApp.Views;

public partial class MainPage : ContentPage
{
    public MainPage() { InitializeComponent(); }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Rider Sign In",
            "OTP-based rider login wires into POST /api/auth/send-otp + /api/auth/verify-otp. " +
            "This is the v3.0 .NET 9 MAUI scaffold — extend with your sign-in flow.",
            "Got it");
    }
}
