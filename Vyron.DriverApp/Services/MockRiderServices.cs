using Vyron.DriverApp.Models;

namespace Vyron.DriverApp.Services;

public sealed class MockRiderAuthService : IRiderAuthService
{
    public Task<RiderLoginResult> LoginAsync(string phoneOrEmail, string password, bool keepSignedIn, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var isValid = !string.IsNullOrWhiteSpace(phoneOrEmail) && !string.IsNullOrWhiteSpace(password);
        return Task.FromResult(isValid
            ? new RiderLoginResult(true)
            : new RiderLoginResult(false, "Invalid credentials. Please check your phone and password."));
    }

    public Task<RiderOnboardingDraft> GetOnboardingDraftAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RiderOnboardingDraft("Chinedu Okafor", "+234 803 412 8821", "LAG-238-XK", "Bike"));
    }
}

public sealed class MockRiderJobService : IRiderJobService
{
    public Task<IReadOnlyList<RiderJobCard>> GetAssignedJobsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<RiderJobCard> jobs =
        [
            new("#VY-2841", "Adaeze Nwosu", "BrightWash Ikeja",
                "12B Allen Ave, Ikeja", "Flat 3A, Opebi Rd",
                "1.8 km", "9 min", "Awaiting pickup", "Pickup"),
            new("#VY-2835", "Emeka Obi", "SdsHub Yaba",
                "SdsHub, Herbert Macaulay", "Adekunle Estate, Yaba",
                "-", "Delivered", "Completed", "Drop-off", true)
        ];

        return Task.FromResult(jobs);
    }

    public Task<IReadOnlyList<RiderProgressStep>> GetProgressAsync(string jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<RiderProgressStep> progress =
        [
            new("✓", "Job accepted", "10:24 AM", true, false),
            new("✓", "En route to customer", "10:31 AM", true, false),
            new("3", "Picked up from customer", "Next step", false, true),
            new("4", "Dropped at store", "-", false, false),
            new("5", "Delivered to customer", "-", false, false)
        ];

        return Task.FromResult(progress);
    }
}

public sealed class MockRiderLocationService : IRiderLocationService
{
    public Task<string> GetCurrentStopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult("BrightWash - Allen Avenue, Ikeja");
    }
}

public sealed class MockRiderNotificationService : IRiderNotificationService
{
    public Task<IReadOnlyList<RiderNotification>> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<RiderNotification> notifications =
        [
            new("□", "New job assigned", "#VY-2841 - Adaeze Nwosu - 2.4 km away", "now", true),
            new("◷", "Pickup reminder", "Order #VY-2840 pickup in 10 mins", "5m", true),
            new("!", "Delayed order alert", "#VY-2835 is 12 min behind ETA", "20m", true),
            new("▭", "Payout processed", "₦42,800 sent to GTBank ****2841", "1h", false),
            new("○", "Customer message", "Adaeze: Please call before arriving", "2h", false),
            new("⌂", "Store update", "BrightWash Ikeja closes early today (6pm)", "Yesterday", false)
        ];

        return Task.FromResult(notifications);
    }
}

public sealed class MockRiderEarningsService : IRiderEarningsService
{
    public Task<IReadOnlyList<PayoutRow>> GetPayoutHistoryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<PayoutRow> payouts =
        [
            new("₦42,800", "May 19 - Bank transfer", "Paid", true),
            new("₦38,100", "May 12 - Bank transfer", "Paid", true),
            new("₦29,400", "May 05 - Bank transfer", "Processing", false)
        ];

        return Task.FromResult(payouts);
    }
}

public sealed class MockRiderProfileService : IRiderProfileService
{
    public Task<IReadOnlyList<RiderOptionRow>> GetAccountOptionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<RiderOptionRow> rows =
        [
            new("▤", "My documents", "3 verified"),
            new("▭", "Payment method", "GTB ****2841"),
            new("✓", "Verification status", "Verified"),
            new("⚙", "Settings", ""),
            new("↪", "Log out", "", true)
        ];

        return Task.FromResult(rows);
    }
}
