using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Orders;

public partial class TrackPage : ContentPage
{
    private readonly TrackViewModel _vm;

    public TrackPage(TrackViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitAsync();
    }
}
