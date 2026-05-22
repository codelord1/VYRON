using Vyron.DriverApp.ViewModels;
using Vyron.DriverApp.Models;

namespace Vyron.DriverApp.Views;

public partial class AssignedOrdersPage : ContentPage
{
    public AssignedOrdersPage(AssignedOrdersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnViewJobClicked(object sender, EventArgs e)
    {
        if (BindingContext is not AssignedOrdersViewModel viewModel ||
            sender is not BindableObject { BindingContext: RiderJobCard job } ||
            !viewModel.ViewJobCommand.CanExecute(job))
            return;

        viewModel.ViewJobCommand.Execute(job);
    }
}
