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
                filter: filter));

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
        => await Shell.Current.GoToAsync($"storeDetails?storeId={store.Id}");

    [RelayCommand]
    private async Task GoToProfileAsync()
        => await Shell.Current.GoToAsync("//profile");
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

        var (data, _) = await SafeCallAsync(() => _stores.GetStoreAsync(id));
        Store      = data;
        HasReviews = Store?.RecentReviews?.Count > 0;
    }

    [RelayCommand]
    private async Task StartOrderAsync()
    {
        if (Store == null) return;
        if (Store.Status != "Active")
        {
            await Shell.Current.DisplayAlert("Store unavailable",
                "This store is not currently accepting orders.", "OK");
            return;
        }
        await Shell.Current.GoToAsync($"serviceSelection?storeId={Store.Id}");
    }
}
