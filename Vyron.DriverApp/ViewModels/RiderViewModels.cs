using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Vyron.DriverApp.Models;

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
}

public partial class LoginViewModel : RiderViewModel
{
    [ObservableProperty] private string _phoneOrEmail = "+234 803 412 8821";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private bool _keepSignedIn = true;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(PhoneOrEmail) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Invalid credentials. Please check your phone and password.";
            OnPropertyChanged(nameof(HasError));
            return;
        }

        await NavigateAsync(RiderRoutes.Home);
    }

    [RelayCommand]
    private async Task ApplyAsync() => await NavigateAsync(RiderRoutes.Onboarding);
}

public partial class RiderOnboardingViewModel : RiderViewModel
{
    [ObservableProperty] private string _fullName = "Chinedu Okafor";
    [ObservableProperty] private string _phoneNumber = "+234 803 412 8821";
    [ObservableProperty] private string _vehiclePlate = "LAG-238-XK";
    [ObservableProperty] private string _selectedVehicle = "Bike";
    public IReadOnlyList<string> Vehicles { get; } = ["Bike", "Bicycle", "Car", "Van"];

    [RelayCommand]
    private void SelectVehicle(string vehicle) => SelectedVehicle = vehicle;

    [RelayCommand]
    private async Task SubmitAsync() => await NavigateAsync(RiderRoutes.Home);
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
    private readonly List<RiderJobCard> _allJobs = [.. RiderSamples.Jobs];
    [ObservableProperty] private ObservableCollection<RiderJobCard> _jobs = [];
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedFilter = "All";
    public AssignedOrdersViewModel() => ApplyFilter();

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
    [ObservableProperty] private string _jobId = "#VY-2841";
    public ObservableCollection<RiderProgressStep> Progress { get; } = RiderSamples.Progress();

    [RelayCommand] private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
    [RelayCommand] private async Task OpenMapAsync() => await NavigateAsync(RiderRoutes.Map);
    [RelayCommand] private async Task StartPickupAsync() => await NavigateAsync(RiderRoutes.ConfirmPickup);
    [RelayCommand] private async Task CallCustomerAsync() =>
        await Shell.Current.DisplayAlert("Call customer", "Calling Adaeze Nwosu.", "OK");
    [RelayCommand] private async Task CallStoreAsync() =>
        await Shell.Current.DisplayAlert("Call store", "Calling BrightWash Ikeja.", "OK");
}

public partial class RiderMapViewModel : RiderViewModel
{
    [RelayCommand] private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
    [RelayCommand] private async Task ArriveAsync() => await NavigateAsync(RiderRoutes.ConfirmPickup);
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
    public ObservableCollection<PayoutRow> Payouts { get; } = RiderSamples.Payouts();
    [RelayCommand] private async Task WithdrawAsync() =>
        await Shell.Current.DisplayAlert("Withdraw", "Withdrawal connects to rider payouts when the endpoint is ready.", "OK");
}

public partial class NotificationsViewModel : RiderViewModel
{
    public ObservableCollection<RiderNotification> Notifications { get; } = RiderSamples.Notifications();
    [RelayCommand] private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
    [RelayCommand] private void MarkAllRead()
    {
        var read = Notifications.Select(note => note with { IsUnread = false }).ToList();
        Notifications.Clear();
        foreach (var note in read)
            Notifications.Add(note);
    }
}

public partial class ProfileViewModel : RiderViewModel
{
    public ObservableCollection<RiderOptionRow> Options { get; } =
    [
        new("▤", "My documents", "3 verified"),
        new("▭", "Payment method", "GTB ****2841"),
        new("✓", "Verification status", "Verified"),
        new("⚙", "Settings", ""),
        new("↪", "Log out", "", true)
    ];

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
