using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Vyron.CustomerApp.DTOs;
using Vyron.CustomerApp.Models;
using Vyron.CustomerApp.Services;

namespace Vyron.CustomerApp.ViewModels;

// ─── SERVICE CARD VIEW MODEL ──────────────────────────────────────
/// <summary>Wraps a ServiceSummaryDto and tracks whether it's in the cart.</summary>
public partial class ServiceCardViewModel : ObservableObject
{
    public ServiceSummaryDto Service { get; }

    [ObservableProperty]
    private bool _isSelected;

    public ServiceCardViewModel(ServiceSummaryDto svc) => Service = svc;
}

public partial class HomeViewModel : BaseViewModel
{
    private readonly StoreService _stores;
    private readonly OrderService _orders;
    private readonly OrderDraftService _draft;
    private DateTime _lastLoadedAt = DateTime.MinValue;
    private bool _hasLoaded;

    [ObservableProperty] private ObservableCollection<StoreListItemDto> _storeItems = new();
    [ObservableProperty] private OrderDto? _activeOrder;
    [ObservableProperty] private bool _hasActiveOrder;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private int _unreadNotificationCount;
    [ObservableProperty] private string? _reorderTitle;
    [ObservableProperty] private string? _reorderSubtitle;
    [ObservableProperty] private Guid? _lastCompletedOrderId;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotTrackingOrder))]
    [NotifyPropertyChangedFor(nameof(IsNotReordering))]
    private bool _isTrackingOrder;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotReordering))]
    private bool _isReordering;

    public bool IsNotTrackingOrder => !IsTrackingOrder;
    public bool IsNotReordering => !IsReordering;
    public bool HasUnreadNotifications => UnreadNotificationCount > 0;
    public bool HasReorder => LastCompletedOrderId.HasValue;

    public string CustomerName
    {
        get
        {
            var name = AppSession.Current.User?.FullName;
            if (string.IsNullOrWhiteSpace(name)) return "there";
            return name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        }
    }

    public string PickupLocation =>
        Preferences.Default.Get("Vyron.Customer.PickupLocation", "Lekki Phase 1");

    public HomeViewModel(StoreService stores, OrderService orders, OrderDraftService draft)
    {
        _stores = stores;
        _orders = orders;
        _draft = draft;
    }

    partial void OnUnreadNotificationCountChanged(int value) =>
        OnPropertyChanged(nameof(HasUnreadNotifications));

    partial void OnLastCompletedOrderIdChanged(Guid? value) =>
        OnPropertyChanged(nameof(HasReorder));

    public async Task InitAsync()
    {
        if (_hasLoaded && DateTime.UtcNow - _lastLoadedAt < TimeSpan.FromMinutes(2))
            return;

        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            IsRefreshing = false;
            return;
        }

        IsBusy = true;
        ClearMessages();
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();

            // Parallel fetch: stores + orders in one round-trip window
            var storesTask = _stores.GetStoresAsync(search: search, sort: "rating");
            var ordersTask = _orders.GetMyOrdersAsync();
            await Task.WhenAll(storesTask, ordersTask);

            var (stores, storesError) = storesTask.Result;
            var (orders, ordersError) = ordersTask.Result;

            if (storesError == "SESSION_EXPIRED" || ordersError == "SESSION_EXPIRED")
            {
                await HandleUnauthorized();
                return;
            }

            if (stores != null)
            {
                StoreItems.Clear();
                foreach (var store in stores.Take(6))
                    StoreItems.Add(store);
            }
            else if (storesError != null)
            {
                SetError(ApiErrorHelper.FriendlyContextMessage(storesError, "stores"));
            }

            if (orders != null)
            {
                ActiveOrder = orders.FirstOrDefault(o => o.Status is not ("Completed" or "Cancelled" or "BalancePaid"));
                HasActiveOrder = ActiveOrder != null;

                var lastCompleted = orders.FirstOrDefault(o => o.Status is "Completed" or "BalancePaid" or "Delivered");
                LastCompletedOrderId = lastCompleted?.Id;
                ReorderTitle = lastCompleted?.Store?.Name != null
                    ? $"Reorder from {lastCompleted.Store.Name}"
                    : null;
                ReorderSubtitle = lastCompleted?.Service?.Name != null
                    ? $"{lastCompleted.Service.Name} · ₦{lastCompleted.TotalEstimate:N0}"
                    : null;
                OnPropertyChanged(nameof(HasReorder));
            }

            UnreadNotificationCount = orders?.Count(o => o.Status is "Ready" or "OutForDelivery" or "Delivered") ?? 0;
            OnPropertyChanged(nameof(HasUnreadNotifications));
            _hasLoaded = true;
            _lastLoadedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            SetError(ApiErrorHelper.ForException(ex));
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task GoToStoresAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                await Shell.Current.GoToAsync(AppRoutes.Stores);
            else
                await Shell.Current.GoToAsync($"{AppRoutes.Stores}?search={Uri.EscapeDataString(SearchText.Trim())}");
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[NAV ERROR] GoToStores: {ex}");
#endif
        }
    }

    [RelayCommand]
    private async Task GoToOrdersAsync()
    {
        try { await Shell.Current.GoToAsync(AppRoutes.Orders); }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[NAV ERROR] GoToOrders: {ex}");
#endif
        }
    }

    [RelayCommand]
    private async Task SelectStoreAsync(StoreListItemDto store)
    {
        if (IsTrackingOrder || store == null)
            return;

        TapFeedback.HapticClick();
        IsTrackingOrder = true;
        try
        {
            await Shell.Current.GoToAsync($"{AppRoutes.StoreDetails}?storeId={store.Id}");
        }
        finally
        {
            IsTrackingOrder = false;
        }
    }

    [RelayCommand]
    private async Task ViewActiveOrderAsync()
    {
        if (IsTrackingOrder)
            return;

        if (ActiveOrder == null)
        {
            await Shell.Current.GoToAsync(AppRoutes.Orders);
            return;
        }

        if (!ApiErrorHelper.HasInternetAccess)
        {
            SetError(ApiErrorHelper.OfflineMessage);
            return;
        }

        IsTrackingOrder = true;
        ClearMessages();
        try
        {
            TapFeedback.HapticClick();
            await Shell.Current.GoToAsync($"{AppRoutes.OrderTracking}?orderId={ActiveOrder.Id}");
        }
        catch (Exception ex)
        {
            SetError(ApiErrorHelper.ForException(ex));
        }
        finally
        {
            IsTrackingOrder = false;
        }
    }

    [RelayCommand]
    private async Task StartPickupAsync()
    {
        TapFeedback.HapticClick();
        await Shell.Current.GoToAsync(AppRoutes.Stores);
    }

    [RelayCommand]
    private async Task QuickServiceAsync(string service)
    {
        if (string.IsNullOrWhiteSpace(service))
            return;

        TapFeedback.HapticClick();
        await Shell.Current.GoToAsync($"{AppRoutes.Stores}?search={Uri.EscapeDataString(service)}");
    }

    [RelayCommand]
    private async Task OpenNotificationsAsync()
    {
        TapFeedback.HapticClick();
        await Shell.Current.GoToAsync(AppRoutes.Notifications);
    }

    [RelayCommand]
    private async Task OpenPickupLocationAsync()
    {
        TapFeedback.HapticClick();
        await Shell.Current.GoToAsync(AppRoutes.PickupLocation);
    }

    [RelayCommand]
    private async Task ApplyPromoAsync()
    {
        TapFeedback.HapticClick();
        await Clipboard.Default.SetTextAsync("VYRON20");
        SuccessMessage = "VYRON20 copied. Apply it when checking out.";
    }

    [RelayCommand]
    private async Task ReorderAsync()
    {
        if (IsReordering)
            return;

        if (LastCompletedOrderId == null)
        {
            await Shell.Current.GoToAsync(AppRoutes.Orders);
            return;
        }

        if (!ApiErrorHelper.HasInternetAccess)
        {
            SetError(ApiErrorHelper.OfflineMessage);
            return;
        }

        IsReordering = true;
        ClearMessages();
        try
        {
            TapFeedback.HapticClick();
            var (draft, _) = await SafeCallAsync(() => _orders.GetReorderDraftAsync(LastCompletedOrderId.Value), "orders");
            if (draft == null)
                return;

            if (!draft.CanReorder || !draft.StoreActive || !draft.ServiceActive)
            {
                SetError("This order cannot be reordered because the store or service is no longer available.");
                return;
            }

            var (store, _) = await SafeCallAsync(() => _stores.GetStoreAsync(draft.StoreId), "stores");
            var service = store?.Services.FirstOrDefault(s => s.Id == draft.ServiceOfferingId && s.IsActive);
            if (service == null)
            {
                SetError("This service is no longer available. Please choose another service.");
                return;
            }

            _draft.LoadReorderDraft(draft, service);
            await Shell.Current.GoToAsync($"{AppRoutes.CreateOrder}?storeId={draft.StoreId}");
        }
        finally
        {
            IsReordering = false;
        }
    }
}

// ─── STORES LIST ─────────────────────────────────────────────────
[QueryProperty(nameof(SearchText), "search")]
public partial class StoresViewModel : BaseViewModel
{
    private readonly StoreService _stores;
    private DateTime _lastLoadedAt = DateTime.MinValue;
    private bool _hasLoaded;
    private Task? _loadTask;

    [ObservableProperty] private ObservableCollection<StoreListItemDto> _storeItems = new();
    [ObservableProperty] private string _searchText    = "";
    [ObservableProperty] private string _selectedFilter = "";   // "", "toprated", "fast", "verified"
    [ObservableProperty] private bool   _isEmpty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotNavigating))]
    [NotifyPropertyChangedFor(nameof(IsOverlayVisible))]
    [NotifyPropertyChangedFor(nameof(OverlayText))]
    private bool _isNavigating;

    private CancellationTokenSource? _searchDebounce;
    public bool IsNotNavigating => !IsNavigating;
    public bool IsOverlayVisible => IsBusy || IsNavigating;
    public string OverlayText => IsNavigating ? "Opening store…" : "Loading stores…";
    public int StoreCount => StoreItems.Count;
    public string StoresSubtitle => $"{StoreCount} verified laundry store{(StoreCount == 1 ? "" : "s")} near you";
    public bool HasSearchContext =>
        !string.IsNullOrWhiteSpace(SearchText) ||
        !string.IsNullOrWhiteSpace(SelectedFilter);
    public string EmptyTitle => HasSearchContext ? "No matching stores found" : "No stores available yet";
    public string EmptySubtitle => HasSearchContext
        ? "Try another service, store name, or location."
        : "Verified laundry stores will appear here.";
    public bool IsNearestFilterSelected => string.IsNullOrWhiteSpace(SelectedFilter);
    public bool IsTopRatedFilterSelected => SelectedFilter == "toprated";
    public bool IsFastestFilterSelected => SelectedFilter == "fast";
    public bool IsCheapestFilterSelected => SelectedFilter == "cheapest";

    // ── Personalised greeting ────────────────────────────────────
    public string Greeting
    {
        get
        {
            var name = AppSession.Current.User?.FullName;
            if (string.IsNullOrWhiteSpace(name)) return "Hi there";
            var first = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            return $"Hi, {first}";
        }
    }

    public string UserInitials
    {
        get
        {
            var name = AppSession.Current.User?.FullName;
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Take(2).Select(p => p[0].ToString().ToUpperInvariant()));
        }
    }

    // Propagate IsBusy → IsOverlayVisible / OverlayText.
    // IsNavigating is handled via [NotifyPropertyChangedFor] on the field.
    // Note: OnPropertyChanged(PropertyChangedEventArgs) is virtual; the string
    // overload is not — so we override the EventArgs form.
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(IsBusy))
        {
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(IsOverlayVisible)));
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(OverlayText)));
        }
    }

    public StoresViewModel(StoreService stores) => _stores = stores;

    public async Task InitAsync()
    {
        await LoadStoresAsync(force: false);
    }

    /// <summary>Debounce live search: fires LoadAsync 400ms after user stops typing.</summary>
    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchContext));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptySubtitle));
        if (!_hasLoaded)
            return;

        _searchDebounce?.Cancel();
        _searchDebounce = new CancellationTokenSource();
        var tok = _searchDebounce.Token;
        Task.Delay(400, tok).ContinueWith(
            _ => MainThread.BeginInvokeOnMainThread(async () => await LoadStoresAsync(force: true)),
            CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
    }

    partial void OnSelectedFilterChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchContext));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptySubtitle));
        OnPropertyChanged(nameof(IsNearestFilterSelected));
        OnPropertyChanged(nameof(IsTopRatedFilterSelected));
        OnPropertyChanged(nameof(IsFastestFilterSelected));
        OnPropertyChanged(nameof(IsCheapestFilterSelected));
    }

    [RelayCommand]
    private async Task LoadAsync() => await LoadStoresAsync(force: true);

    [RelayCommand]
    private async Task RefreshAsync() => await LoadStoresAsync(force: true);

    private async Task LoadStoresAsync(bool force)
    {
        if (!force && _hasLoaded && DateTime.UtcNow - _lastLoadedAt < TimeSpan.FromMinutes(2))
        {
            IsRefreshing = false;
            return;
        }

        if (_loadTask is { IsCompleted: false })
        {
            IsRefreshing = false;
            return;
        }

        _loadTask = LoadStoresCoreAsync();
        await _loadTask;
    }

    private async Task LoadStoresCoreAsync()
    {
        if (IsBusy)
        {
            IsRefreshing = false;
            return;
        }

        // "toprated" → sort by rating, do NOT filter by IsTopRated flag (flag may be empty)
        var sort = SelectedFilter switch
        {
            "toprated" => "rating",
            "cheapest" => "price",
            _ => null
        };
        var filter = SelectedFilter switch
        {
            "fast"     => "fast",
            "verified" => "verified",
            _          => (string?)null   // "toprated" handled by sort only
        };

        try
        {
            var (data, _) = await SafeCallAsync(() =>
                _stores.GetStoresAsync(
                    search: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                    sort: sort,
                    filter: filter), "stores");

            if (data != null)
            {
                var stores = data.AsEnumerable();
                if (SelectedFilter == "cheapest")
                    stores = stores.OrderBy(s => s.Services.Where(x => x.IsActive).Select(x => x.BasePrice).DefaultIfEmpty(decimal.MaxValue).Min());

                StoreItems.Clear();
                foreach (var s in stores)
                    StoreItems.Add(s);

                NotifyStoreSummaryChanged();
            }

            IsEmpty = StoreItems.Count == 0;
            _hasLoaded = true;
            _lastLoadedAt = DateTime.UtcNow;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        _searchDebounce?.Cancel();
        await LoadStoresAsync(force: true);
    }

    [RelayCommand]
    private async Task ClearSearchAsync()
    {
        _searchDebounce?.Cancel();
        SearchText = "";
        SelectedFilter = "";
        _lastLoadedAt = DateTime.MinValue;
        await LoadStoresAsync(force: true);
    }

    [RelayCommand]
    private async Task SetFilterAsync(string value)
    {
        if (SelectedFilter == value && StoreItems.Count > 0)
            return;

        _searchDebounce?.Cancel();
        SelectedFilter = value;
        _lastLoadedAt = DateTime.MinValue;
        await LoadStoresAsync(force: true);
    }

    private void NotifyStoreSummaryChanged()
    {
        OnPropertyChanged(nameof(StoreCount));
        OnPropertyChanged(nameof(StoresSubtitle));
    }

    [RelayCommand]
    private async Task SelectStoreAsync(StoreListItemDto store)
    {
        if (IsNavigating || store == null)
            return;

        TapFeedback.HapticClick();
        IsNavigating = true;
        try
        {
            await Shell.Current.GoToAsync($"{AppRoutes.StoreDetails}?storeId={store.Id}");
        }
        finally
        {
            IsNavigating = false;
        }
    }

    [RelayCommand]
    private async Task OrderNowAsync(StoreListItemDto store)
    {
        if (IsNavigating || store == null)
            return;

        TapFeedback.HapticClick();
        IsNavigating = true;
        try
        {
            await Shell.Current.GoToAsync($"{AppRoutes.ServiceSelection}?storeId={store.Id}");
        }
        finally
        {
            IsNavigating = false;
        }
    }

    [RelayCommand]
    private async Task GoToProfileAsync()
        => await Shell.Current.GoToAsync(AppRoutes.Profile);
}

// ─── STORE DETAILS ───────────────────────────────────────────────
[QueryProperty(nameof(StoreId), "storeId")]
public partial class StoreDetailsViewModel : BaseViewModel
{
    private readonly StoreService _stores;

    [ObservableProperty] private string          _storeId   = "";
    [ObservableProperty] private StoreDetailDto? _store;
    [ObservableProperty] private bool            _hasReviews;

    // ── Page-lock for navigation actions ───────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotNavigating))]
    [NotifyPropertyChangedFor(nameof(IsOverlayVisible))]
    [NotifyPropertyChangedFor(nameof(OverlayText))]
    private bool _isNavigating;

    // ── Local favourite toggle (persisted to Preferences) ──────
    [ObservableProperty] private bool _isFavorite;

    public bool IsNotNavigating  => !IsNavigating;
    public bool IsOverlayVisible => IsBusy || IsNavigating;
    public string OverlayText    => IsNavigating ? "Opening store…" : "Loading store…";

    // ── Quick-stats helpers (computed from Store) ───────────────
    public string PickupFeeText    => Store?.PickupFee == 0 ? "Free" : Store?.PickupFeeDisplay ?? "—";
    public string DeliveryFeeText  => Store?.DeliveryFee == 0 ? "Free" : Store?.DeliveryFeeDisplay ?? "—";
    public string TurnaroundText   => Store != null ? $"~{Store.EstimatedPickupMinutes} min" : "—";
    public string ServiceCountText => Store != null
        ? $"{Store.Services.Count(s => s.IsActive)} service{(Store.Services.Count(s => s.IsActive) == 1 ? "" : "s")}"
        : "";

    // Propagate IsBusy → IsOverlayVisible / OverlayText
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(IsBusy))
        {
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(IsOverlayVisible)));
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(OverlayText)));
        }
    }

    public StoreDetailsViewModel(StoreService stores) => _stores = stores;

    partial void OnStoreIdChanged(string value)
    {
        if (!Guid.TryParse(value, out _)) return;
        // Restore persisted favourite state for this store
        IsFavorite = Preferences.Default.Get($"fav_{value}", false);
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try { await LoadAsync(); }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[StoreDetailsViewModel] OnStoreIdChanged load error: {ex}");
#endif
                SetError(ApiErrorHelper.ForException(ex));
            }
        });
    }

    partial void OnStoreChanged(StoreDetailDto? value)
    {
        // Notify all quick-stat computed properties when Store loads
        OnPropertyChanged(nameof(PickupFeeText));
        OnPropertyChanged(nameof(DeliveryFeeText));
        OnPropertyChanged(nameof(TurnaroundText));
        OnPropertyChanged(nameof(ServiceCountText));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            IsRefreshing = false;
            return;
        }

        if (!Guid.TryParse(StoreId, out var id))
        {
            IsRefreshing = false;
            return;
        }

        try
        {
            var (data, _) = await SafeRefreshCallAsync(() => _stores.GetStoreAsync(id), "stores");
            if (data != null)
            {
                Store = data;
                HasReviews = Store?.RecentReviews?.Count > 0;
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task StartOrderAsync()
    {
        if (Store == null || IsNavigating) return;
        if (Store.Status != "Active" || !Store.IsCurrentlyOpen)
        {
            await Shell.Current.DisplayAlert("Store closed",
                "This store is currently closed and not accepting orders. Please check back later.", "OK");
            return;
        }
        TapFeedback.HapticClick();
        IsNavigating = true;
        try
        {
            await Shell.Current.GoToAsync($"{AppRoutes.ServiceSelection}?storeId={Store.Id}");
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[NAV ERROR] StartOrder: {ex}");
#endif
            SetError("Unable to open service selection. Please try again.");
        }
        finally
        {
            IsNavigating = false;
        }
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
        TapFeedback.HapticClick();
        if (!string.IsNullOrEmpty(StoreId))
            Preferences.Default.Set($"fav_{StoreId}", IsFavorite);
    }

    [RelayCommand]
    private static async Task GoBackAsync()
        => await Shell.Current.GoToAsync("..");
}
