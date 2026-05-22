using Vyron.DriverApp.ViewModels;

namespace Vyron.DriverApp.Views;

public partial class AssignedOrdersPage : ContentPage
{
    public AssignedOrdersPage(AssignedOrdersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
