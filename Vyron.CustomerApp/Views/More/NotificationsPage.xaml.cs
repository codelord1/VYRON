using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.More;

public partial class NotificationsPage : ContentPage
{
    public NotificationsPage(NotificationsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is NotificationsViewModel vm)
            await vm.InitAsync();
    }
}
