using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Reviews;

public partial class AddReviewPage : ContentPage
{
    private readonly AddReviewViewModel _vm;

    public AddReviewPage(AddReviewViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    private void OnStarTap(object sender, EventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.CommandParameter?.ToString(), out var stars))
            _vm.Rating = stars;
    }

    private async void OnBackToOrders(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("//orders");
}
