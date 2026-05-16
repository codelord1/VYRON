using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Auth;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
