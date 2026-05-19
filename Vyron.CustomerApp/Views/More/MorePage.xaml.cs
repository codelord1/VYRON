using Vyron.CustomerApp.ViewModels;
using Vyron.CustomerApp.Services;

namespace Vyron.CustomerApp.Views.More;

public partial class MorePage : ContentPage
{
    private readonly MoreViewModel _vm;

    public MorePage(MoreViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.RefreshUserInfo();
    }

    private async void OnPaymentMethodsTapped(object sender, TappedEventArgs e)
    {
        TapFeedback.HapticClick();
        await DisplayAlert("Coming soon",
            "Payment methods and wallet features will be available in a future release.",
            "OK");
    }
}
