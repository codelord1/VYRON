using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Orders;

public partial class OrdersPage : ContentPage
{
    private readonly OrdersViewModel _vm;

    public OrdersPage(OrdersViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitAsync();
    }

    // Also refresh when navigated to via Shell route (e.g. from More tab "Order History")
    // because OnAppearing may not fire if the page was already in the tab's history.
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await _vm.InitAsync();
    }
}
