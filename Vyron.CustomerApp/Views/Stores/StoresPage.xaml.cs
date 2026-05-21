using System.ComponentModel;
using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Stores;

public partial class StoresPage : ContentPage
{
    private readonly StoresViewModel _vm;
    private bool _isSpinning;

    public StoresPage(StoresViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitAsync();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(StoresViewModel.IsOverlayVisible))
            return;

        if (_vm.IsOverlayVisible && !_isSpinning)
            _ = SpinAsync();
    }

    /// <summary>
    /// Continuously rotates the branded icon while the overlay is visible.
    /// Stops cleanly once IsOverlayVisible goes false (no cancel token needed —
    /// RotateTo resolves and the while-check exits on the next iteration).
    /// </summary>
    private async Task SpinAsync()
    {
        _isSpinning = true;
        LoadingIcon.Rotation = 0;
        while (_vm.IsOverlayVisible)
        {
            await LoadingIcon.RotateTo(360, 1200, Easing.Linear);
            LoadingIcon.Rotation = 0;
        }
        _isSpinning = false;
    }
}
