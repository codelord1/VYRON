using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Auth;

public partial class VerifyOtpPage : ContentPage
{
    public VerifyOtpPage(VerifyOtpViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
