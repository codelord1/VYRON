using Vyron.DriverApp.ViewModels;

namespace Vyron.DriverApp.Views;

public partial class OrderDetailsPage : ContentPage
{
    public OrderDetailsPage(OrderDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
