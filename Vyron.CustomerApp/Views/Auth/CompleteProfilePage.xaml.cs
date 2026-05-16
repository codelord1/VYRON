using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Auth;

public partial class CompleteProfilePage : ContentPage
{
    public CompleteProfilePage(CompleteProfileViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnBackToLoginTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");
}
