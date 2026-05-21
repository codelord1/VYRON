using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Orders;

public partial class OrderDetailsPage : ContentPage
{
    private readonly OrderDetailsViewModel _vm;

    public OrderDetailsPage(OrderDetailsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Loading is triggered by OnOrderIdChanged → BeginInvokeOnMainThread in the ViewModel.
    // Pull-to-refresh is bound to LoadCommand. No OnAppearing load needed.
    protected override void OnAppearing() => base.OnAppearing();
}
