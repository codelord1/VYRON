using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Orders;

public partial class OrderSuccessPage : ContentPage
{
    public OrderSuccessPage(OrderSuccessViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
