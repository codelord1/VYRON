using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Auth;

public partial class SignupPage : ContentPage
{
    public SignupPage(SignupViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnLoginTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");
}
