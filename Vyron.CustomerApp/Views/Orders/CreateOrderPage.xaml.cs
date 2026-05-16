using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Orders;

public partial class CreateOrderPage : ContentPage
{
    public CreateOrderPage(CreateOrderViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
