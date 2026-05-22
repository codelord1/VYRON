using System.ComponentModel;
using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Orders;

public partial class ServiceSelectionPage : ContentPage
{
    private readonly ServiceSelectionViewModel _vm;
    private bool _isSpinning;

    public ServiceSelectionPage(ServiceSelectionViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Unsubscribe to prevent the VM from holding a reference to this page
        // (both are Transient, but the delegate keeps this page alive until the VM is also released).
        _vm.PropertyChanged -= OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ServiceSelectionViewModel.IsOverlayVisible)) return;
        if (_vm.IsOverlayVisible && !_isSpinning) _ = SpinAsync();
    }

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
