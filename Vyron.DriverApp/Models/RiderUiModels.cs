using System.Collections.ObjectModel;

namespace Vyron.DriverApp.Models;

public sealed record RiderJobCard(
    string Id,
    string Customer,
    string Store,
    string PickupAddress,
    string DropoffAddress,
    string Distance,
    string Eta,
    string Status,
    string Kind,
    bool IsCompleted = false);

public sealed record RiderNotification(
    string Icon,
    string Title,
    string Message,
    string Time,
    bool IsUnread);

public sealed record PayoutRow(
    string Amount,
    string Subtitle,
    string Status,
    bool IsPaid);

public sealed record RiderProgressStep(
    string Number,
    string Title,
    string Time,
    bool IsDone,
    bool IsCurrent);

public sealed record RiderOptionRow(
    string Icon,
    string Title,
    string Value,
    bool IsDanger = false);

public static class RiderSamples
{
    public static IReadOnlyList<RiderJobCard> Jobs { get; } =
    [
        new("#VY-2841", "Adaeze Nwosu", "BrightWash Ikeja",
            "12B Allen Ave, Ikeja", "Flat 3A, Opebi Rd",
            "1.8 km", "9 min", "Awaiting pickup", "Pickup"),
        new("#VY-2835", "Emeka Obi", "SdsHub Yaba",
            "SdsHub, Herbert Macaulay", "Adekunle Estate, Yaba",
            "-", "Delivered", "Completed", "Drop-off", true)
    ];

    public static ObservableCollection<RiderNotification> Notifications() =>
    [
        new("□", "New job assigned", "#VY-2841 - Adaeze Nwosu - 2.4 km away", "now", true),
        new("◷", "Pickup reminder", "Order #VY-2840 pickup in 10 mins", "5m", true),
        new("!", "Delayed order alert", "#VY-2835 is 12 min behind ETA", "20m", true),
        new("▭", "Payout processed", "₦42,800 sent to GTBank ****2841", "1h", false),
        new("○", "Customer message", "Adaeze: Please call before arriving", "2h", false),
        new("⌂", "Store update", "BrightWash Ikeja closes early today (6pm)", "Yesterday", false)
    ];

    public static ObservableCollection<PayoutRow> Payouts() =>
    [
        new("₦42,800", "May 19 - Bank transfer", "Paid", true),
        new("₦38,100", "May 12 - Bank transfer", "Paid", true),
        new("₦29,400", "May 05 - Bank transfer", "Processing", false)
    ];

    public static ObservableCollection<RiderProgressStep> Progress() =>
    [
        new("✓", "Job accepted", "10:24 AM", true, false),
        new("✓", "En route to customer", "10:31 AM", true, false),
        new("3", "Picked up from customer", "Next step", false, true),
        new("4", "Dropped at store", "-", false, false),
        new("5", "Delivered to customer", "-", false, false)
    ];
}
