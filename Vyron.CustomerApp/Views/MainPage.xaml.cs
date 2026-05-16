namespace Vyron.CustomerApp.Views;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnGetStartedClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Coming soon",
            "Sign-in, store browser, order placement, tracking, and reviews are all wired into the API. " +
            "This is the v3.0 .NET 9 MAUI scaffold — extend with your XAML pages.",
            "Got it");
    }
}
