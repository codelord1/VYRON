using Vyron.DriverApp.ViewModels;

namespace Vyron.DriverApp.Views;

public partial class RiderMapPage : ContentPage
{
    public RiderMapPage(RiderMapViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
