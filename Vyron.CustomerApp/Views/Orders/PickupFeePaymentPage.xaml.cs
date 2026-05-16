using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Orders;

public partial class PickupFeePaymentPage : ContentPage
{
    public PickupFeePaymentPage(PickupFeeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
