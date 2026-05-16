using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Auth;

public partial class ResetPasswordPage : ContentPage
{
    public ResetPasswordPage(ResetPasswordViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
