using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Orders;

public partial class ServiceSelectionPage : ContentPage
{
    public ServiceSelectionPage(ServiceSelectionViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
