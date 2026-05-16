using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Disputes;

public partial class DisputeHistoryPage : ContentPage
{
    public DisputeHistoryPage(DisputeHistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await ((DisputeHistoryViewModel)BindingContext).InitAsync();
    }
}
