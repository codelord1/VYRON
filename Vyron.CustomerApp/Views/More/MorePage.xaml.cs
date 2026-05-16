using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.More;

public partial class MorePage : ContentPage
{
    private readonly MoreViewModel _vm;

    public MorePage(MoreViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.RefreshUserInfo();
    }
}
