using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Orders;

public partial class TrackPage : ContentPage
{
    private readonly TrackViewModel _vm;

    public TrackPage(TrackViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _vm.InitAsync();
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[TRACKPAGE APPEARING ERROR] {ex}");
#endif
            // Exception already handled inside LoadAsync/SetError.
            // This guard prevents async void from propagating to the Android looper.
        }
    }
}
