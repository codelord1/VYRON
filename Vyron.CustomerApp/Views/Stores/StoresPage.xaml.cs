using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Stores;

public partial class StoresPage : ContentPage
{
    private readonly StoresViewModel _vm;

    public StoresPage(StoresViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitAsync();
    }
}
