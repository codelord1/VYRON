using Vyron.DriverApp.ViewModels;

namespace Vyron.DriverApp.Views;

public partial class ConfirmPickupPage : ContentPage
{
    public ConfirmPickupPage(ConfirmPickupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
