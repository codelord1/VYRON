using Vyron.DriverApp.ViewModels;

namespace Vyron.DriverApp.Views;

public partial class RiderSettingsPage : ContentPage
{
    public RiderSettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
