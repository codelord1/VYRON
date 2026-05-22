using Vyron.DriverApp.ViewModels;

namespace Vyron.DriverApp.Views;

public partial class EarningsPage : ContentPage
{
    public EarningsPage(EarningsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
