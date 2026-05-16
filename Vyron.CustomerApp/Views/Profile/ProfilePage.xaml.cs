using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Profile;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _vm;

    public ProfilePage(ProfileViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitAsync();
    }

    private void OnEditClicked(object sender, EventArgs e) => _vm.EditMode = true;
}
