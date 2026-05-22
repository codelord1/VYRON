using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Vyron.DriverApp.Models;
using Vyron.DriverApp.Services;

namespace Vyron.DriverApp.ViewModels;

public abstract partial class RiderViewModel : ObservableObject
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    protected async Task NavigateAsync(string route)
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await Shell.Current.GoToAsync(route);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
            target.Add(item);
    }
}

public partial class LoginViewModel(IRiderAuthService authService) : RiderViewModel
{
    [ObservableProperty] private string _phoneOrEmail = "+234 803 412 8821";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private bool _keepSignedIn = true;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            var result = await authService.LoginAsync(PhoneOrEmail, Password, KeepSignedIn);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            await Shell.Current.GoToAsync(RiderRoutes.Home);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasError));
        }
    }

    [RelayCommand]
    private async Task ApplyAsync() => await NavigateAsync(RiderRoutes.Onboarding);
}

public partial class RiderOnboardingViewModel : RiderViewModel
{
    private readonly IRiderAuthService _authService;

    [ObservableProperty] private string _fullName = "";
    [ObservableProperty] private string _phoneNumber = "";
    [ObservableProperty] private string _vehiclePlate = "";
    [ObservableProperty] private string _selectedVehicle = "Bike";
    public IReadOnlyList<string> Vehicles { get; } = ["Bike", "Bicycle", "Car", "Van"];

    public RiderOnboardingViewModel(IRiderAuthService authService)
    {
        _authService = authService;
        _ = LoadDraftAsync();
    }

    [RelayCommand]
    private void SelectVehicle(string vehicle) => SelectedVehicle = vehicle;

    [RelayCommand]
    private async Task SubmitAsync() => await NavigateAsync(RiderRoutes.Home);

    private async Task LoadDraftAsync()
    {
        var draft = await _authService.GetOnboardingDraftAsync();
        FullName = draft.FullName;
        PhoneNumber = draft.PhoneNumber;
        VehiclePlate = draft.VehiclePlate;
        SelectedVehicle = draft.VehicleType;
    }
}

public partial class RiderHomeViewModel : RiderViewModel
{
    [ObservableProperty] private bool _isOnline = true;
    public string RiderName => "Chinedu O.";
    public string Initials => "CO";

    [RelayCommand] private async Task OpenOrdersAsync() => await NavigateAsync(RiderRoutes.Orders);
    [RelayCommand] private async Task OpenMapAsync() => await NavigateAsync(RiderRoutes.Map);
    [RelayCommand] private async Task OpenEarningsAsync() => await NavigateAsync(RiderRoutes.Earnings);
    [RelayCommand] private async Task OpenNotificationsAsync() => await NavigateAsync(RiderRoutes.Notifications);
    [RelayCommand] private async Task OpenSupportAsync() =>
        await Shell.Current.DisplayAlert("Vyron Support", "Support chat is ready for API integration.", "OK");
}

public partial class AssignedOrdersViewModel : RiderViewModel
{
    private readonly IRiderJobService _jobService;
    private readonly List<RiderJobCard> _allJobs = [];

    [ObservableProperty] private ObservableCollection<RiderJobCard> _jobs = [];
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedFilter = "All";

    public AssignedOrdersViewModel(IRiderJobService jobService)
    {
        _jobService = jobService;
        _ = LoadJobsAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void SelectFilter(string filter)
    {
        SelectedFilter = filter;
        ApplyFilter();
    }

    [RelayCommand]
    private async Task ViewJobAsync(RiderJobCard job) =>
        await NavigateAsync($"{RiderRoutes.OrderDetails}?jobId={Uri.EscapeDataString(job.Id)}");

    private async Task LoadJobsAsync()
    {
        var jobs = await _jobService.GetAssignedJobsAsync();
        _allJobs.Clear();
        _allJobs.AddRange(jobs);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<RiderJobCard> source = _allJobs;
        if (!string.Equals(SelectedFilter, "All", StringComparison.Ordinal))
        {
            source = SelectedFilter switch
            {
                "Completed" => source.Where(job => job.IsCompleted),
                "In Progress" => source.Where(job => !job.IsCompleted),
                _ => source.Where(job => string.Equals(job.Kind, SelectedFilter, StringComparison.Ordinal))
            };
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            source = source.Where(job =>
                job.Id.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                job.Customer.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                job.Store.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        Jobs = new ObservableCollection<RiderJobCard>(source);
    }
}

[QueryProperty(nameof(JobId), "jobId")]
public partial class OrderDetailsViewModel : RiderViewModel
{
    private readonly IRiderJobService _jobService;

    [ObservableProperty] private string _jobId = "#VY-2841";
    public ObservableCollection<RiderProgressStep> Progress { get; } = [];

    public OrderDetailsViewModel(IRiderJobService jobService)
    {
        _jobService = jobService;
        _ = LoadProgressAsync(JobId);
    }

    partial void OnJobIdChanged(string value) => _ = LoadProgressAsync(value);

    [RelayCommand] private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
    [RelayCommand] private async Task OpenMapAsync() => await NavigateAsync(RiderRoutes.Map);
    [RelayCommand] private async Task StartPickupAsync() => await NavigateAsync(RiderRoutes.ConfirmPickup);
    [RelayCommand] private async Task CallCustomerAsync() =>
        await Shell.Current.DisplayAlert("Call customer", "Calling Adaeze Nwosu.", "OK");
    [RelayCommand] private async Task CallStoreAsync() =>
        await Shell.Current.DisplayAlert("Call store", "Calling BrightWash Ikeja.", "OK");

    private async Task LoadProgressAsync(string jobId)
    {
        var progress = await _jobService.GetProgressAsync(jobId);
        ReplaceItems(Progress, progress);
    }
}

public partial class RiderMapViewModel : RiderViewModel
{
    private readonly IRiderLocationService _locationService;

    [ObservableProperty] private string _currentStop = "BrightWash - Allen Avenue, Ikeja";

    public RiderMapViewModel(IRiderLocationService locationService)
    {
        _locationService = locationService;
        _ = LoadStopAsync();
    }

    [RelayCommand] private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
    [RelayCommand] private async Task ArriveAsync() => await NavigateAsync(RiderRoutes.ConfirmPickup);

    private async Task LoadStopAsync() => CurrentStop = await _locationService.GetCurrentStopAsync();
}

public partial class ConfirmPickupViewModel : RiderViewModel
{
    [ObservableProperty] private int _bagCount = 1;
    [ObservableProperty] private string _notes = "Customer mentioned 2 silk items - hand wash.";

    [RelayCommand] private void AddBag() => BagCount++;
    [RelayCommand] private void RemoveBag() { if (BagCount > 1) BagCount--; }
    [RelayCommand] private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
    [RelayCommand] private async Task ConfirmAsync() => await NavigateAsync(RiderRoutes.Delivered);
}

public partial class DeliveredViewModel : RiderViewModel
{
    [RelayCommand] private async Task ViewOrderAsync() => await NavigateAsync(RiderRoutes.OrderDetails);
    [RelayCommand] private async Task NextJobAsync() => await NavigateAsync(RiderRoutes.Orders);
    [RelayCommand] private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
}

public partial class EarningsViewModel : RiderViewModel
{
    private readonly IRiderEarningsService _earningsService;
    public ObservableCollection<PayoutRow> Payouts { get; } = [];

    public EarningsViewModel(IRiderEarningsService earningsService)
    {
        _earningsService = earningsService;
        _ = LoadPayoutsAsync();
    }

    [RelayCommand] private async Task WithdrawAsync() =>
        await Shell.Current.DisplayAlert("Withdraw", "Withdrawal connects to rider payouts when the endpoint is ready.", "OK");

    private async Task LoadPayoutsAsync() => ReplaceItems(Payouts, await _earningsService.GetPayoutHistoryAsync());
}

public partial class NotificationsViewModel : RiderViewModel
{
    private readonly IRiderNotificationService _notificationService;
    public ObservableCollection<RiderNotification> Notifications { get; } = [];

    public NotificationsViewModel(IRiderNotificationService notificationService)
    {
        _notificationService = notificationService;
        _ = LoadNotificationsAsync();
    }

    [RelayCommand] private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
    [RelayCommand] private void MarkAllRead()
    {
        var read = Notifications.Select(note => note with { IsUnread = false }).ToList();
        ReplaceItems(Notifications, read);
    }

    private async Task LoadNotificationsAsync() => ReplaceItems(Notifications, await _notificationService.GetNotificationsAsync());
}

public partial class ProfileViewModel : RiderViewModel
{
    private readonly IRiderProfileService _profileService;
    public ObservableCollection<RiderOptionRow> Options { get; } = [];

    public ProfileViewModel(IRiderProfileService profileService)
    {
        _profileService = profileService;
        _ = LoadOptionsAsync();
    }

    [RelayCommand]
    private async Task OpenSettingsAsync() => await NavigateAsync(RiderRoutes.Settings);

    [RelayCommand]
    private async Task OpenOptionAsync(RiderOptionRow row)
    {
        if (row.Title == "Settings")
            await NavigateAsync(RiderRoutes.Settings);
        else if (row.Title == "Log out")
            await NavigateAsync(RiderRoutes.Login);
        else
            await Shell.Current.DisplayAlert(row.Title, $"{row.Title} is ready for backend integration.", "OK");
    }

    private async Task LoadOptionsAsync() => ReplaceItems(Options, await _profileService.GetAccountOptionsAsync());
}

public partial class SettingsViewModel : RiderViewModel
{
    [ObservableProperty] private bool _darkMode;
    [ObservableProperty] private bool _pushNotifications = true;
    [ObservableProperty] private bool _locationPermission = true;

    partial void OnDarkModeChanged(bool value)
    {
        if (Application.Current != null)
            Application.Current.UserAppTheme = value ? AppTheme.Dark : AppTheme.Unspecified;
    }

    [RelayCommand] private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
}
