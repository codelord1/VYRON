using Vyron.DriverApp.ViewModels;

namespace Vyron.DriverApp.Views;

public partial class RiderNotificationsPage : ContentPage
{
    public RiderNotificationsPage(NotificationsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
