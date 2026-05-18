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

    [ObservableProperty] private ObservableCollection<StoreListItemDto> _storeItems = new();
    [ObservableProperty] private OrderDto? _activeOrder;
    [ObservableProperty] private bool _hasActiveOrder;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotTrackingOrder))]
    private bool _isTrackingOrder;

    public bool IsNotTrackingOrder => !IsTrackingOrder;

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

    public HomeViewModel(StoreService stores, OrderService orders)
    {
        _stores = stores;
        _orders = orders;
    }

    public async Task InitAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;

        var (stores, _) = await SafeCallAsync(() => _stores.GetStoresAsync(sort: "rating"), "stores");
        StoreItems.Clear();
        foreach (var store in stores?.Take(6) ?? Enumerable.Empty<StoreListItemDto>())
            StoreItems.Add(store);

        var (orders, _) = await SafeCallAsync(() => _orders.GetMyOrdersAsync(), "orders");
        ActiveOrder = orders?.FirstOrDefault(o => o.Status is not ("Completed" or "Cancelled" or "BalancePaid"));
        HasActiveOrder = ActiveOrder != null;

        IsBusy = false;
    }

    [RelayCommand]
    private async Task GoToStoresAsync() => await Shell.Current.GoToAsync(AppRoutes.Stores);

    [RelayCommand]
    private async Task GoToOrdersAsync() => await Shell.Current.GoToAsync(AppRoutes.Orders);

    [RelayCommand]
    private async Task SelectStoreAsync(StoreListItemDto store)
        => await Shell.Current.GoToAsync($"{AppRoutes.StoreDetails}?storeId={store.Id}");

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
    private async Task StartPickupAsync() => await Shell.Current.GoToAsync(AppRoutes.Stores);
}

// ─── STORES LIST ─────────────────────────────────────────────────
public partial class StoresViewModel : BaseViewModel
{
    private readonly StoreService _stores;

    [ObservableProperty] private ObservableCollection<StoreListItemDto> _storeItems = new();
    [ObservableProperty] private string _searchText    = "";
    [ObservableProperty] private string _selectedFilter = "";   // "", "toprated", "fast", "verified"
    [ObservableProperty] private bool   _isEmpty;

    private CancellationTokenSource? _searchDebounce;

    // ── Personalised greeting ────────────────────────────────────
    public string Greeting
    {
        get
        {
            var name = AppSession.Current.User?.FullName;
            if (string.IsNullOrWhiteSpace(name)) return "Hi there 👋";
            var first = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            return $"Hi, {first} 👋";
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

    public StoresViewModel(StoreService stores) => _stores = stores;

    public async Task InitAsync() => await LoadAsync();

    /// <summary>Debounce live search: fires LoadAsync 400ms after user stops typing.</summary>
    partial void OnSearchTextChanged(string value)
    {
        _searchDebounce?.Cancel();
        _searchDebounce = new CancellationTokenSource();
        var tok = _searchDebounce.Token;
        Task.Delay(400, tok).ContinueWith(
            _ => MainThread.BeginInvokeOnMainThread(async () => await LoadAsync()),
            CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        // "toprated" → sort by rating, do NOT filter by IsTopRated flag (flag may be empty)
        var sort = SelectedFilter == "toprated" ? "rating" : null;
        var filter = SelectedFilter switch
        {
            "fast"     => "fast",
            "verified" => "verified",
            _          => (string?)null   // "toprated" handled by sort only
        };

        var (data, _) = await SafeCallAsync(() =>
            _stores.GetStoresAsync(
                search: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                sort:   sort,
                filter: filter), "stores");

        StoreItems.Clear();
        foreach (var s in data ?? Enumerable.Empty<StoreListItemDto>())
            StoreItems.Add(s);

        IsEmpty = StoreItems.Count == 0;
    }

    [RelayCommand]
    private async Task SearchAsync() => await LoadAsync();

    [RelayCommand]
    private async Task SetFilterAsync(string value)
    {
        SelectedFilter = value;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SelectStoreAsync(StoreListItemDto store)
        => await Shell.Current.GoToAsync($"{AppRoutes.StoreDetails}?storeId={store.Id}");

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

    public StoreDetailsViewModel(StoreService stores) => _stores = stores;

    partial void OnStoreIdChanged(string value)
    {
        if (Guid.TryParse(value, out _))
            MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(StoreId, out var id)) return;

        var (data, _) = await SafeCallAsync(() => _stores.GetStoreAsync(id), "stores");
        Store      = data;
        HasReviews = Store?.RecentReviews?.Count > 0;
    }

    [RelayCommand]
    private async Task StartOrderAsync()
    {
        if (Store == null) return;
        if (Store.Status != "Active" || !Store.IsCurrentlyOpen)
        {
            await Shell.Current.DisplayAlert("Store closed",
                "This store is currently closed and not accepting orders. Please check back later.", "OK");
            return;
        }
        await Shell.Current.GoToAsync($"{AppRoutes.ServiceSelection}?storeId={Store.Id}");
    }
}
