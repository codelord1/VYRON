using Vyron.DriverApp.ViewModels;
using Vyron.DriverApp.Models;

namespace Vyron.DriverApp.Views;

public partial class RiderProfilePage : ContentPage
{
    public RiderProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnOptionTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is not ProfileViewModel viewModel ||
            sender is not BindableObject { BindingContext: RiderOptionRow option } ||
            !viewModel.OpenOptionCommand.CanExecute(option))
            return;

        viewModel.OpenOptionCommand.Execute(option);
    }
}
