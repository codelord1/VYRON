using Vyron.DriverApp.Models;

namespace Vyron.DriverApp.Services;

// API-backed implementations can replace these Rider-only service seams later
// without changing Rider page bindings or navigation commands.
public interface IRiderAuthService
{
    Task<RiderLoginResult> LoginAsync(string phoneOrEmail, string password, bool keepSignedIn, CancellationToken cancellationToken = default);
    Task<RiderOnboardingDraft> GetOnboardingDraftAsync(CancellationToken cancellationToken = default);
}

public interface IRiderJobService
{
    Task<IReadOnlyList<RiderJobCard>> GetAssignedJobsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RiderProgressStep>> GetProgressAsync(string jobId, CancellationToken cancellationToken = default);
}

public interface IRiderLocationService
{
    Task<string> GetCurrentStopAsync(CancellationToken cancellationToken = default);
}

public interface IRiderNotificationService
{
    Task<IReadOnlyList<RiderNotification>> GetNotificationsAsync(CancellationToken cancellationToken = default);
}

public interface IRiderEarningsService
{
    Task<IReadOnlyList<PayoutRow>> GetPayoutHistoryAsync(CancellationToken cancellationToken = default);
}

public interface IRiderProfileService
{
    Task<IReadOnlyList<RiderOptionRow>> GetAccountOptionsAsync(CancellationToken cancellationToken = default);
}
