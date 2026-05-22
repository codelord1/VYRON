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

public sealed record RiderLoginResult(
    bool IsSuccess,
    string? ErrorMessage = null);

public sealed record RiderOnboardingDraft(
    string FullName,
    string PhoneNumber,
    string VehiclePlate,
    string VehicleType);
