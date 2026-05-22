using Vyron.DriverApp.ViewModels;

namespace Vyron.DriverApp.Views;

public partial class RiderHomePage : ContentPage
{
    public RiderHomePage(RiderHomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
