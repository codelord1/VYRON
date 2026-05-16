using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Stores;

public partial class StoreDetailsPage : ContentPage
{
    public StoreDetailsPage(StoreDetailsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
