using Vyron.DriverApp.ViewModels;

namespace Vyron.DriverApp.Views;

public partial class RiderProfilePage : ContentPage
{
    public RiderProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
