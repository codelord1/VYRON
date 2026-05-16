using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Vyron.CustomerApp.DTOs;
using Vyron.CustomerApp.Services;

namespace Vyron.CustomerApp.ViewModels;

// ─── SERVICE SELECTION ───────────────────────────────────────────
[QueryProperty(nameof(StoreId), "storeId")]
public partial class ServiceSelectionViewModel : BaseViewModel
{
    private readonly StoreService      _stores;
    private readonly OrderDraftService _draft;

    [ObservableProperty] private string _storeId = "";
    [ObservableProperty] private StoreDetailDto? _store;

    // Service cards with IsSelected state
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDraftItems))]
    private ObservableCollection<ServiceCardViewModel> _serviceCards = new();

    // Expose the draft's item collection directly for XAML binding
    public ObservableCollection<OrderDraftItem> DraftItems => _draft.Items;

    // Computed summary (refreshed whenever cart changes)
    [ObservableProperty] private string _estimateSummary = "";
    [ObservableProperty] private decimal _totalLaundryCost;
    [ObservableProperty] private decimal _pickupFee;
    [ObservableProperty] private decimal _deliveryFee;
    [ObservableProperty] private decimal _totalEstimate;
    [ObservableProperty] private decimal _amountDueNow;
    [ObservableProperty] private decimal _balanceDueLater;

    public bool HasDraftItems => _draft.HasItems;

    public ServiceSelectionViewModel(StoreService stores, OrderDraftService draft)
    {
        _stores = stores;
        _draft  = draft;
        // Refresh totals + selected state whenever items are added or removed
        _draft.Items.CollectionChanged += (_, e) =>
        {
            // Subscribe to each newly added item so weight/pieces changes also refresh totals
            if (e.NewItems != null)
                foreach (Services.OrderDraftItem item in e.NewItems)
                    item.PropertyChanged += (_, _) => RefreshTotals();
            RefreshTotals();
            UpdateSelectedCards();
            OnPropertyChanged(nameof(HasDraftItems));
        };
    }

    partial void OnStoreIdChanged(string value)
    {
        if (Guid.TryParse(value, out _))
            MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(StoreId, out var id)) return;
        var (data, _) = await SafeCallAsync(() => _stores.GetStoreAsync(id));
        Store = data;
        if (Store != null)
        {
            _draft.SetStore(id, Store.Name, Store.PickupFee, Store.DeliveryFee);
            // Build service card wrappers
            ServiceCards.Clear();
            foreach (var svc in Store.Services.Where(s => s.IsActive))
                ServiceCards.Add(new ServiceCardViewModel(svc));
            RefreshTotals();
        }
    }

    /// <summary>Add a service to the cart (or increment quantity if already added).</summary>
    [RelayCommand]
    private void AddService(ServiceCardViewModel card)
    {
        _draft.AddService(card.Service);
        card.IsSelected = true;
        RefreshTotals();
        ClearMessages();
    }

    /// <summary>Remove an item from the cart.</summary>
    [RelayCommand]
    private void RemoveItem(OrderDraftItem item)
    {
        _draft.RemoveItem(item);
        RefreshTotals();
        UpdateSelectedCards();
    }

    /// <summary>Sync IsSelected on all service cards from the draft.</summary>
    private void UpdateSelectedCards()
    {
        foreach (var card in ServiceCards)
            card.IsSelected = _draft.ContainsService(card.Service.Id);
    }

    private void RefreshTotals()
    {
        TotalLaundryCost = _draft.TotalLaundryCost;
        PickupFee        = _draft.PickupFee;
        DeliveryFee      = _draft.DeliveryFee;
        TotalEstimate    = _draft.TotalEstimate;
        AmountDueNow     = _draft.AmountDueNow;
        BalanceDueLater  = _draft.BalanceDueLater;
        EstimateSummary  = _draft.Breakdown;
    }

    [RelayCommand]
    private async Task ContinueAsync()
    {
        if (!_draft.HasItems) { SetError("Please add at least one service."); return; }
        await Shell.Current.GoToAsync($"createOrder?storeId={StoreId}");
    }
}

// ─── CREATE ORDER ────────────────────────────────────────────────
[QueryProperty(nameof(StoreId), "storeId")]
public partial class CreateOrderViewModel : BaseViewModel
{
    private readonly OrderService      _orders;
    private readonly OrderDraftService _draft;

    [ObservableProperty] private string _storeId = "";

    // Expose draft items for the checkout items list
    public ObservableCollection<OrderDraftItem> DraftItems => _draft.Items;
    public string StoreName       => _draft.StoreName;
    public bool    HasDraftItems    => _draft.HasItems;
    public decimal TotalLaundryCost => _draft.TotalLaundryCost;
    public decimal PickupFee        => _draft.PickupFee;
    public decimal DeliveryFee      => _draft.DeliveryFee;
    public decimal TotalEstimate    => _draft.TotalEstimate;
    public decimal AmountDueNow     => _draft.AmountDueNow;
    public decimal BalanceDueLater  => _draft.BalanceDueLater;

    [ObservableProperty] private string _pickupAddress = "";
    [ObservableProperty] private string _deliveryAddress = "";
    [ObservableProperty] private bool _sameAddress = true;
    [ObservableProperty] private string _specialInstructions = "";
    [ObservableProperty] private DateTime _pickupDate = DateTime.Today.AddDays(1);
    [ObservableProperty] private string _selectedSlot = "Morning";
    [ObservableProperty] private string _paymentMethod = "CashOnDelivery";

    public List<string> TimeSlots      { get; } = new() { "Morning", "Afternoon", "Evening" };
    public List<string> PaymentMethods { get; } = new() { "CashOnDelivery", "BankTransfer" };

    public CreateOrderViewModel(OrderService orders, OrderDraftService draft)
    { _orders = orders; _draft = draft; }

    partial void OnSameAddressChanged(bool value)
    {
        if (value) DeliveryAddress = PickupAddress;
    }
    partial void OnPickupAddressChanged(string value)
    {
        if (SameAddress) DeliveryAddress = value;
    }

    [RelayCommand]
    private async Task PlaceOrderAsync()
    {
        if (!_draft.HasItems)
        { SetError("No services selected. Go back and add services."); return; }
        if (string.IsNullOrWhiteSpace(PickupAddress))
        { SetError("Please enter your pickup address."); return; }
        if (!SameAddress && string.IsNullOrWhiteSpace(DeliveryAddress))
        { SetError("Please enter your delivery address."); return; }

        var req = _draft.BuildRequest(
            pickupAddress  : PickupAddress.Trim(),
            deliveryAddress: SameAddress ? PickupAddress.Trim() : DeliveryAddress.Trim(),
            pickupDate     : PickupDate,
            pickupSlot     : SelectedSlot,
            paymentMethod  : PaymentMethod,
            notes          : SpecialInstructions.Trim());

        var (order, _) = await SafeCallAsync(() => _orders.CreateOrderAsync(req));
        if (order != null)
        {
            _draft.Clear();   // cart is submitted — clear draft
            await Shell.Current.GoToAsync($"orderSuccess?orderId={order.Id}");
        }
    }
}

// ─── ORDER SUCCESS ───────────────────────────────────────────────
[QueryProperty(nameof(OrderId), "orderId")]
public partial class OrderSuccessViewModel : BaseViewModel
{
    private readonly OrderService _orders;

    [ObservableProperty] private string _orderId = "";
    [ObservableProperty] private OrderDto? _order;

    public OrderSuccessViewModel(OrderService orders) => _orders = orders;

    partial void OnOrderIdChanged(string value)
    {
        if (Guid.TryParse(value, out _))
            MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());
    }

    private async Task LoadAsync()
    {
        if (!Guid.TryParse(OrderId, out var id)) return;
        var (data, _) = await SafeCallAsync(() => _orders.GetOrderAsync(id));
        Order = data;
    }

    [RelayCommand]
    private async Task PayPickupFeeAsync()
    {
        if (Order != null)
            await Shell.Current.GoToAsync($"pickupFeePayment?orderId={Order.Id}");
    }

    [RelayCommand]
    private async Task GoToOrdersAsync()
        => await Shell.Current.GoToAsync(AppRoutes.Orders);
}

// ─── PICKUP FEE PAYMENT ──────────────────────────────────────────
[QueryProperty(nameof(OrderId), "orderId")]
public partial class PickupFeeViewModel : BaseViewModel
{
    private readonly OrderService _orders;
    private readonly PaymentService _payments;

    [ObservableProperty] private string _orderId = "";
    [ObservableProperty] private OrderDto? _order;
    [ObservableProperty] private string _selectedMethod = "CashOnDelivery";
    [ObservableProperty] private bool _paymentDone;

    public List<string> PaymentMethods { get; } = new() { "CashOnDelivery", "BankTransfer" };

    public PickupFeeViewModel(OrderService orders, PaymentService payments)
    { _orders = orders; _payments = payments; }

    partial void OnOrderIdChanged(string value)
    {
        if (Guid.TryParse(value, out _))
            MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());
    }

    private async Task LoadAsync()
    {
        if (!Guid.TryParse(OrderId, out var id)) return;
        var (data, _) = await SafeCallAsync(() => _orders.GetOrderAsync(id));
        Order = data;
    }

    [RelayCommand]
    private async Task ConfirmPaymentAsync()
    {
        if (Order == null) return;

        // Guard: already paid — don't submit twice
        if (Order.PaymentState == "PickupFeePaid" ||
            Order.Status == "PickupFeePaid" ||
            Order.Status == "RiderAssigned" ||
            Order.Status == "PickedUp")
        {
            SetError("Pickup fee has already been paid for this order.");
            return;
        }

        // Call service directly (not SafeCallAsync) — SafeCallAsync returns (data, bool handled)
        // and the 'bool handled' was being compared to null which is always false (CS0472).
        var capturedOrderId = Order.Id;
        var capturedFee     = Order.PickupFeeAmount;
        ClearMessages();
        IsBusy = true;
        try
        {
            var (payResult, payError) = await _payments.PayPickupFeeAsync(capturedOrderId, capturedFee, SelectedMethod);

            if (payResult != null)
            {
                // Success path
                PaymentDone = true;
                SuccessMessage = $"Pickup fee of ₦{capturedFee:N0} recorded! A rider will be assigned shortly.";
                // Reload order to reflect new PaymentState/Status
                var (updated, _) = await _orders.GetOrderAsync(capturedOrderId);
                if (updated != null) Order = updated;
                // Wait briefly so user sees success banner, then jump to Track tab
                await Task.Delay(1800);
                await Shell.Current.GoToAsync(AppRoutes.Track);
            }
            else if (payError != null && payError.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                // Already paid — treat as success and navigate rather than show error
                PaymentDone = true;
                SuccessMessage = "Pickup fee already recorded. Navigating to tracker…";
                await Task.Delay(900);
                await Shell.Current.GoToAsync(AppRoutes.Track);
            }
            else
            {
                ErrorMessage = payError ?? "Payment failed. Please try again.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task TrackOrderAsync()
        // Navigate to the Track tab — shows active order card
        => await Shell.Current.GoToAsync(AppRoutes.Track);
}

// ─── ORDERS LIST ─────────────────────────────────────────────────
public partial class OrdersViewModel : BaseViewModel
{
    private readonly OrderService _orders;

    [ObservableProperty] private ObservableCollection<OrderDto> _orders_ = new();
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private int _currentPage = 1;

    public OrdersViewModel(OrderService orders) => _orders = orders;

    public async Task InitAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var (data, _) = await SafeCallAsync(() => _orders.GetMyOrdersAsync(CurrentPage));
        Orders_.Clear();
        foreach (var o in data ?? Enumerable.Empty<OrderDto>())
            Orders_.Add(o);
        IsEmpty = Orders_.Count == 0;
    }

    [RelayCommand]
    private async Task SelectOrderAsync(OrderDto order)
        => await Shell.Current.GoToAsync($"orderDetails?orderId={order.Id}");
}

// ─── ORDER DETAILS ───────────────────────────────────────────────
[QueryProperty(nameof(OrderId), "orderId")]
public partial class OrderDetailsViewModel : BaseViewModel
{
    private readonly OrderService _orders;

    [ObservableProperty] private string _orderId = "";
    [ObservableProperty] private OrderDto? _order;

    public OrderDetailsViewModel(OrderService orders) => _orders = orders;

    partial void OnOrderIdChanged(string value)
    {
        if (Guid.TryParse(value, out _))
            MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(OrderId, out var id)) return;
        var (data, _) = await SafeCallAsync(() => _orders.GetOrderAsync(id));
        Order = data;
    }

    [RelayCommand]
    private async Task PayPickupFeeAsync()
    {
        if (Order != null)
            await Shell.Current.GoToAsync($"pickupFeePayment?orderId={Order.Id}");
    }

    [RelayCommand]
    private async Task PayBalanceAsync()
    {
        if (Order != null)
            await Shell.Current.GoToAsync($"balancePayment?orderId={Order.Id}");
    }

    [RelayCommand]
    private async Task RaiseDisputeAsync()
    {
        if (Order != null)
            await Shell.Current.GoToAsync($"raiseDispute?orderId={Order.Id}");
    }

    [RelayCommand]
    private async Task AddReviewAsync()
    {
        if (Order != null)
            await Shell.Current.GoToAsync($"addReview?orderId={Order.Id}&storeId={Order.Store.Id}");
    }
}

// ─── BALANCE PAYMENT ─────────────────────────────────────────────
[QueryProperty(nameof(OrderId), "orderId")]
public partial class BalancePaymentViewModel : BaseViewModel
{
    private readonly OrderService _orders;
    private readonly PaymentService _payments;

    [ObservableProperty] private string _orderId = "";
    [ObservableProperty] private OrderDto? _order;
    [ObservableProperty] private string _selectedMethod = "CashOnDelivery";
    [ObservableProperty] private bool _paymentDone;

    public List<string> PaymentMethods { get; } = new() { "CashOnDelivery", "BankTransfer" };

    public BalancePaymentViewModel(OrderService orders, PaymentService payments)
    { _orders = orders; _payments = payments; }

    partial void OnOrderIdChanged(string value)
    {
        if (Guid.TryParse(value, out _))
            MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());
    }

    private async Task LoadAsync()
    {
        if (!Guid.TryParse(OrderId, out var id)) return;
        var (data, _) = await SafeCallAsync(() => _orders.GetOrderAsync(id));
        Order = data;
    }

    [RelayCommand]
    private async Task ConfirmPaymentAsync()
    {
        if (Order == null) return;

        // Same bug fix as PickupFeeViewModel: SafeCallAsync returns bool not string.
        var capturedOrderId = Order.Id;
        var capturedBalance = Order.BalanceAmount;
        ClearMessages();
        IsBusy = true;
        try
        {
            var (balResult, balError) = await _payments.PayBalanceAsync(capturedOrderId, capturedBalance, SelectedMethod);

            if (balResult != null)
            {
                PaymentDone = true;
                SuccessMessage = $"Balance of ₦{capturedBalance:N0} paid! Thank you for using VYRON.";
                var (updated, _) = await _orders.GetOrderAsync(capturedOrderId);
                if (updated != null) Order = updated;
            }
            else if (balError != null && balError.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                PaymentDone = true;
                SuccessMessage = "Balance already recorded. Order is complete!";
                var (updated, _) = await _orders.GetOrderAsync(capturedOrderId);
                if (updated != null) Order = updated;
            }
            else
            {
                ErrorMessage = balError ?? "Payment failed. Please try again.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }
}
