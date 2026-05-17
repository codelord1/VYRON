using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Orders;

public partial class BalancePaymentPage : ContentPage
{
    public BalancePaymentPage(BalancePaymentViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnBackToOrders(object sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.Orders);
}
