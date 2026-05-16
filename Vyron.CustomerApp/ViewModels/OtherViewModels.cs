using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Vyron.CustomerApp.DTOs;
using Vyron.CustomerApp.Models;
using Vyron.CustomerApp.Services;

namespace Vyron.CustomerApp.ViewModels;

// ─── ORDER PROGRESS STEP (used in TrackPage progress tree) ───────
public class OrderProgressStep
{
    public string Label     { get; set; } = "";
    public string Icon      { get; set; } = "";
    public string? Timestamp{ get; set; }
    public bool IsDone      { get; set; }
    public bool IsCurrent   { get; set; }
    public bool IsFuture    { get; set; }
    public bool HasConnector{ get; set; } = true;
}

// ─── TRACK (active order live status) ────────────────────────────
public partial class TrackViewModel : BaseViewModel
{
    private readonly OrderService _orders;

    // Active statuses — must match the API's OrderStatus enum names exactly
    private static readonly HashSet<string> ActiveStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pending", "Confirmed", "PickupFeePaid", "RiderAssigned",
        "PickedUp", "Processing", "Ready", "OutForDelivery", "Delivered"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private OrderDto? _activeOrder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _hasActiveOrder;

    [ObservableProperty]
    private List<OrderProgressStep> _progressSteps = new();

    public bool IsEmpty => !HasActiveOrder && !IsBusy;

    public TrackViewModel(OrderService orders) => _orders = orders;

    public async Task InitAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        var (orders, _) = await SafeCallAsync(() => _orders.GetMyOrdersAsync());
        var active = orders?.FirstOrDefault(o => ActiveStatuses.Contains(o.Status));
        ActiveOrder = active;
        HasActiveOrder = active != null;
        ProgressSteps = active != null ? BuildProgressSteps(active) : new();
        IsBusy = false;
    }

    private static readonly (string Status, string Icon, string Label)[] StepDefs =
    {
        ("Pending",        "🕐", "Order placed"),
        ("Confirmed",      "✅", "Order confirmed"),
        ("PickupFeePaid",  "💳", "Pickup fee paid"),
        ("RiderAssigned",  "🏍", "Rider assigned"),
        ("PickedUp",       "📦", "Laundry picked up"),
        ("Processing",     "🧺", "Being cleaned"),
        ("Ready",          "✨", "Ready for delivery"),
        ("OutForDelivery", "🚚", "Out for delivery"),
        ("Delivered",      "🏠", "Delivered"),
    };

    private static List<OrderProgressStep> BuildProgressSteps(OrderDto order)
    {
        // Determine current step index
        var currentIndex = Array.FindIndex(StepDefs, s => s.Status == order.Status);
        if (currentIndex < 0) currentIndex = 0;

        // Build a lookup of timestamps from status history
        var tsLookup = order.StatusHistory.ToDictionary(
            h => h.Status, h => h.ChangedAt.ToLocalTime().ToString("d MMM, h:mm tt"));

        var steps = new List<OrderProgressStep>();
        for (int i = 0; i < StepDefs.Length; i++)
        {
            var (status, icon, label) = StepDefs[i];
            tsLookup.TryGetValue(status, out var ts);
            steps.Add(new OrderProgressStep
            {
                Label        = label,
                Icon         = icon,
                Timestamp    = ts,
                IsDone       = i < currentIndex,
                IsCurrent    = i == currentIndex,
                IsFuture     = i > currentIndex,
                HasConnector = i < StepDefs.Length - 1
            });
        }
        return steps;
    }

    [RelayCommand]
    private async Task ViewOrderAsync()
    {
        if (ActiveOrder == null) return;
        await Shell.Current.GoToAsync($"orderDetails?orderId={ActiveOrder.Id}");
    }

    [RelayCommand]
    private async Task GoToStoresAsync()
        => await Shell.Current.GoToAsync(AppRoutes.Stores);

    /// <summary>Open native phone dialer to call the store.</summary>
    [RelayCommand]
    private async Task CallStoreAsync()
    {
        var phone = ActiveOrder?.Store?.Phone;
        if (string.IsNullOrWhiteSpace(phone)) return;
        try { await Launcher.Default.OpenAsync(new Uri($"tel:{phone}")); }
        catch { await Shell.Current.DisplayAlert("Error", "Could not open phone dialer.", "OK"); }
    }

    /// <summary>Open native phone dialer to call the assigned rider.</summary>
    [RelayCommand]
    private async Task CallRiderAsync()
    {
        var phone = ActiveOrder?.Rider?.Phone;
        if (string.IsNullOrWhiteSpace(phone)) return;
        try { await Launcher.Default.OpenAsync(new Uri($"tel:{phone}")); }
        catch { await Shell.Current.DisplayAlert("Error", "Could not open phone dialer.", "OK"); }
    }

    /// <summary>Navigate to the Message Rider page.</summary>
    [RelayCommand]
    private async Task MessageRiderAsync()
    {
        if (ActiveOrder == null) return;
        await Shell.Current.GoToAsync($"messageRider?orderId={ActiveOrder.Id}");
    }
}

// ─── MORE (account menu tab) ─────────────────────────────────────
public partial class MoreViewModel : BaseViewModel
{
    private readonly IAuthService _auth;

    [ObservableProperty] private string _userName    = "VYRON User";
    [ObservableProperty] private string _userInitials = "V";
    [ObservableProperty] private string _userPhone   = "";

    public MoreViewModel(IAuthService auth) => _auth = auth;

    public void RefreshUserInfo()
    {
        var user = AppSession.Current.User;
        if (user == null) return;

        UserName  = string.IsNullOrWhiteSpace(user.FullName) ? "VYRON User" : user.FullName;
        UserPhone = user.Phone ?? "";

        var parts = UserName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        UserInitials = string.Concat(parts.Take(2).Select(p => p[0].ToString().ToUpperInvariant()));
        if (string.IsNullOrEmpty(UserInitials)) UserInitials = "V";
    }

    [RelayCommand]
    private async Task GoToProfileAsync()
        => await Shell.Current.GoToAsync("profile");

    [RelayCommand]
    private async Task GoToOrderHistoryAsync()
        => await Shell.Current.GoToAsync(AppRoutes.Orders);

    [RelayCommand]
    private async Task GoToDisputeHistoryAsync()
        => await Shell.Current.GoToAsync("disputeHistory");

    [RelayCommand]
    private async Task GoToNotificationsAsync()
        => await Shell.Current.GoToAsync("notifications");

    [RelayCommand]
    private async Task LogoutAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Sign Out", "Are you sure you want to sign out?", "Sign Out", "Cancel");
        if (!confirm) return;

        await _auth.LogoutAsync();
        await Shell.Current.GoToAsync("//login", animate: false);
    }
}

// ─── DISPUTE HISTORY ─────────────────────────────────────────────
public partial class DisputeHistoryViewModel : BaseViewModel
{
    private readonly DisputeService _disputes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private ObservableCollection<DisputeDetailDto> _items = new();

    public bool IsEmpty => Items.Count == 0 && !IsBusy;

    public DisputeHistoryViewModel(DisputeService disputes) => _disputes = disputes;

    public async Task InitAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var (data, _) = await SafeCallAsync(() => _disputes.GetMyDisputesAsync());
        Items.Clear();
        foreach (var d in data ?? Enumerable.Empty<DisputeDetailDto>())
            Items.Add(d);
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
}

// ─── RAISE DISPUTE ───────────────────────────────────────────────
[QueryProperty(nameof(OrderId), "orderId")]
public partial class RaiseDisputeViewModel : BaseViewModel
{
    private readonly DisputeService _disputes;

    [ObservableProperty] private string _orderId = "";
    [ObservableProperty] private string _selectedType = "WrongPricing";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private bool _submitted;

    public List<string> DisputeTypes { get; } = new()
    {
        "WrongPricing", "Delay", "MissingClothes", "DamagedClothes",
        "PoorQuality", "PaymentIssue", "Other"
    };

    public RaiseDisputeViewModel(DisputeService disputes) => _disputes = disputes;

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Description))
        { SetError("Please describe the issue."); return; }
        if (!Guid.TryParse(OrderId, out var orderId))
        { SetError("Invalid order."); return; }

        var (data, _) = await SafeCallAsync(() =>
            _disputes.CreateAsync(new CreateDisputeRequest(orderId, SelectedType, Description.Trim(), null)));

        if (data != null)
        {
            Submitted = true;
            SuccessMessage = "Dispute submitted. We'll review it shortly.";
        }
    }

    [RelayCommand]
    private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
}

// ─── ADD REVIEW ──────────────────────────────────────────────────
[QueryProperty(nameof(OrderId), "orderId")]
[QueryProperty(nameof(StoreId), "storeId")]
public partial class AddReviewViewModel : BaseViewModel
{
    private readonly ReviewService _reviews;

    [ObservableProperty] private string _orderId = "";
    [ObservableProperty] private string _storeId = "";
    [ObservableProperty] private int _rating = 5;
    [ObservableProperty] private string _comment = "";
    [ObservableProperty] private bool _submitted;

    public string StarsDisplay => new string('★', Rating) + new string('☆', 5 - Rating);

    public AddReviewViewModel(ReviewService reviews) => _reviews = reviews;

    partial void OnRatingChanged(int value) => OnPropertyChanged(nameof(StarsDisplay));

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (!Guid.TryParse(OrderId, out var orderId))
        { SetError("Invalid order."); return; }

        var (data, _) = await SafeCallAsync(() =>
            _reviews.CreateAsync(new CreateReviewRequest(orderId, Rating,
                string.IsNullOrWhiteSpace(Comment) ? null : Comment.Trim())));

        if (data != null)
        {
            Submitted = true;
            SuccessMessage = "Thank you for your review!";
        }
    }
}

// ─── NOTIFICATIONS ────────────────────────────────────────────────
public partial class NotificationsViewModel : BaseViewModel
{
    private readonly NotificationService _notifications;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private ObservableCollection<NotificationDto> _items = new();

    [ObservableProperty] private int _unreadCount;

    public bool IsEmpty => Items.Count == 0 && !IsBusy;

    public NotificationsViewModel(NotificationService notifications) => _notifications = notifications;

    public async Task InitAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        var (data, _) = await SafeCallAsync(() => _notifications.GetMyNotificationsAsync());
        Items.Clear();
        foreach (var n in data ?? Enumerable.Empty<NotificationDto>())
            Items.Add(n);
        UnreadCount = Items.Count(n => !n.IsRead);
        OnPropertyChanged(nameof(IsEmpty));
        IsBusy = false;
    }

    [RelayCommand]
    private async Task MarkReadAsync(NotificationDto notification)
    {
        if (notification.IsRead) return;
        await _notifications.MarkReadAsync(notification.Id);
        notification.IsRead = true;
        UnreadCount = Items.Count(n => !n.IsRead);
    }

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        await _notifications.MarkAllReadAsync();
        foreach (var n in Items) n.IsRead = true;
        UnreadCount = 0;
    }

    [RelayCommand]
    private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
}

// ─── MESSAGE RIDER ────────────────────────────────────────────────
[QueryProperty(nameof(OrderId), "orderId")]
public partial class MessageRiderViewModel : BaseViewModel
{
    private readonly RiderMessageService _riderMessages;

    [ObservableProperty] private string _orderId = "";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private bool _sent;

    public MessageRiderViewModel(RiderMessageService riderMessages) => _riderMessages = riderMessages;

    [RelayCommand]
    private async Task SendAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Message))
        { SetError("Please enter a message."); return; }

        if (!Guid.TryParse(OrderId, out var orderId))
        { SetError("Invalid order."); return; }

        IsBusy = true;
        var (data, error) = await _riderMessages.SendAsync(orderId, Message.Trim());
        IsBusy = false;

        if (data != null)
        {
            Sent = true;
            SuccessMessage = "Message sent to your rider!";
            Message = "";
        }
        else
        {
            SetError(error ?? "Failed to send message. Please try again.");
        }
    }

    [RelayCommand]
    private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
}

// ─── PROFILE ─────────────────────────────────────────────────────
public partial class ProfileViewModel : BaseViewModel
{
    private readonly ProfileService _profile;
    private readonly IAuthService _auth;

    [ObservableProperty] private string _fullName = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private bool _editMode;

    public ProfileViewModel(ProfileService profile, IAuthService auth)
    { _profile = profile; _auth = auth; }

    public async Task InitAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var (data, _) = await SafeCallAsync(() => _profile.GetProfileAsync());
        if (data != null)
        {
            FullName = data.FullName;
            Phone = data.Phone;
            Email = data.Email ?? "";
        }
        else if (AppSession.Current.User != null)
        {
            // Fallback to session data
            FullName = AppSession.Current.User.FullName;
            Phone = AppSession.Current.User.Phone;
            Email = AppSession.Current.User.Email ?? "";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FullName))
        { SetError("Full name is required."); return; }

        var (data, _) = await SafeCallAsync(() =>
            _profile.UpdateProfileAsync(FullName.Trim(), Email.Trim()));

        if (data != null)
        {
            SuccessMessage = "Profile updated.";
            EditMode = false;
            if (AppSession.Current.User != null)
            {
                AppSession.Current.User.FullName = data.FullName;
                AppSession.Current.User.Email = data.Email;
            }
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Sign Out", "Are you sure you want to sign out?", "Sign Out", "Cancel");
        if (!confirm) return;

        await _auth.LogoutAsync();
        await Shell.Current.GoToAsync("//login", animate: false);
    }
}
