using Vyron.DriverApp.ViewModels;

namespace Vyron.DriverApp.Views;

public partial class DeliveredPage : ContentPage
{
    public DeliveredPage(DeliveredViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
