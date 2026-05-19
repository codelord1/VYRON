namespace Vyron.CustomerApp.DTOs;

// â”€â”€â”€ AUTH â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
public record SendOtpRequest(string Phone);
public record VerifyOtpRequest(string Phone, string Code, string? FullName);
public record RefreshTokenRequest(string RefreshToken);

// â”€â”€â”€ CUSTOMER AUTH (password-based + OTP signup/reset) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
public record CustomerLoginRequest(string Phone, string Password);
public record SignupOtpRequest(string Phone);
public record CompleteProfileRequest(string Phone, string Code, string FullName, string? Email, string Password);
public record PasswordResetOtpRequest(string Phone);
public record ResetPasswordRequest(string Phone, string Code, string NewPassword);

/// <summary>
/// Response for POST /api/customer-auth/login and /api/customer-auth/complete-profile.
/// Identical shape to AuthResponse â€” kept separate so the API can evolve independently.
/// </summary>
public class CustomerLoginResponse
{
    public string   AccessToken  { get; set; } = "";
    public string   RefreshToken { get; set; } = "";
    public DateTime ExpiresAt    { get; set; }
    public UserDto  User         { get; set; } = new();
}

/// <summary>Response for OTP-send endpoints (signup and password-reset).</summary>
public class OtpSentResponse
{
    public string  Message { get; set; } = "";
    public string? DevOtp  { get; set; }   // non-null only in Development
}

/// <summary>
/// Response from POST /api/auth/send-otp.
/// DevOtp is populated only when the API is running in Development mode
/// (Otp:ReturnOtpInDevelopment = true). It is null in Production.
/// </summary>
public class SendOtpResponse
{
    public bool    Success { get; set; }
    public string  Message { get; set; } = "";
    public string? DevOtp  { get; set; }   // null in Production â€” NEVER display in prod builds
}

/// <summary>
/// Response from POST /api/auth/verify-otp.
/// IsNewUser: phone's very first verification.
/// RequiresProfileCompletion: user has no FullName â€” route to CompleteProfile screen.
/// </summary>
public class VerifyOtpResponse
{
    public string   AccessToken              { get; set; } = "";
    public string   RefreshToken             { get; set; } = "";
    public DateTime ExpiresAt                { get; set; }
    public UserDto  User                     { get; set; } = new();
    public bool     IsNewUser                { get; set; }
    public bool     RequiresProfileCompletion{ get; set; }
}

// â”€â”€â”€ COUNTRY CODE â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
/// <summary>Country code option shown in the phone-number country picker.</summary>
public class CountryCodeOption
{
    public string FlagEmoji   { get; init; } = "";
    public string CountryName { get; init; } = "";
    public string DialCode    { get; init; } = "";   // e.g. "+234"
    public string CountryIso  { get; init; } = "";   // e.g. "NG"

    /// Full display string shown in the picker: "ðŸ‡³ðŸ‡¬ Nigeria +234"
    public string DisplayText => $"{FlagEmoji} {CountryName} {DialCode}";

    /// Short label shown after selection: "ðŸ‡³ðŸ‡¬ +234"
    public string ShortLabel  => $"{FlagEmoji} {DialCode}";

    public override string ToString() => DisplayText;
}

public class AuthResponse
{
    public string   AccessToken  { get; set; } = "";
    public string   RefreshToken { get; set; } = "";
    public DateTime ExpiresAt    { get; set; }
    public UserDto  User         { get; set; } = new();
}

public class UserDto
{
    public Guid      Id        { get; set; }
    public string    Phone     { get; set; } = "";
    public string    FullName  { get; set; } = "";
    public string?   Email     { get; set; }
    public string    Role      { get; set; } = "";
    public DateTime  CreatedAt { get; set; }
}

// â”€â”€â”€ PROFILE â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
public class ProfileDto
{
    public Guid      Id           { get; set; }
    public string    FullName     { get; set; } = "";
    public string    Phone        { get; set; } = "";
    public string?   Email        { get; set; }
    public string    Role         { get; set; } = "";
    public string?   ProfilePhoto { get; set; }
    public DateTime  CreatedAt    { get; set; }
}

public record UpdateProfileRequest(string? FullName, string? Email);

// â”€â”€â”€ STORE â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
public class StoreListItemDto
{
    public Guid   Id                      { get; set; }
    public string Name                    { get; set; } = "";
    public string Description             { get; set; } = "";
    public string Address                 { get; set; } = "";
    public string Area                    { get; set; } = "";
    public decimal AverageRating          { get; set; }
    public int    TotalReviews            { get; set; }
    public int    TotalOrders             { get; set; }
    public decimal PickupFee              { get; set; }
    public decimal DeliveryFee            { get; set; }
    public int    EstimatedPickupMinutes  { get; set; }
    public bool   IsVerified              { get; set; }
    public bool   IsTopRated              { get; set; }
    public bool   FastPickup              { get; set; }
    public string Status                  { get; set; } = "";
    public string? LogoUrl               { get; set; }
    public double Latitude                { get; set; }
    public double Longitude               { get; set; }
    public List<ServiceSummaryDto> Services { get; set; } = new();
    public bool   IsCurrentlyOpen { get; set; } = true;

    // â”€â”€ Display helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public string BadgeText =>
        IsVerified && IsTopRated ? "Verified · Top Rated"
        : IsVerified             ? "Verified"
        : IsTopRated             ? "Top Rated"
        : FastPickup             ? "Fast Pickup"
        : "";

    public string RatingDisplay        => TotalReviews > 0
        ? $"Rating {AverageRating:F1} ({TotalReviews})"
        : "No reviews yet";
    public string EstimatedTimeDisplay => $"~{EstimatedPickupMinutes} min";
    public string PickupFeeDisplay     => $"₦{PickupFee:N0} pickup";
    public string DeliveryFeeDisplay   => $"₦{DeliveryFee:N0} delivery";

    public bool   IsOpen         => Status == "Active" && IsCurrentlyOpen;
    public string OpenStatusText => IsOpen ? "Open" : "Closed";

    public string ServiceCountText => Services.Count switch
    {
        0 => "No services",
        1 => "1 service",
        _ => $"{Services.Count} services"
    };

    public string? StartingFromText => Services.Any(s => s.IsActive)
        ? $" · from ₦{Services.Where(s => s.IsActive).Min(s => s.BasePrice):N0}"
        : null;

    // â”€â”€ Image helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    /// Fully-qualified URL, resolving relative paths against the API base.
    public string? FullLogoUrl  => ResolveImageUrl(LogoUrl);
    public bool    HasLogo      => !string.IsNullOrEmpty(LogoUrl);

    /// Single initial letter for the placeholder tile.
    public string NameInitial => Name.Length > 0
        ? Name.Substring(0, 1).ToUpperInvariant()
        : "?";

    private static string? ResolveImageUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            // Android emulator: the API may return localhost URLs â€” rewrite to API host
            var apiHost = new Uri(AppConstants.ApiBaseUrl).Host;
            url = System.Text.RegularExpressions.Regex.Replace(
                url, @"://localhost(:\d+)?", $"://{apiHost}$1",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return url;
        }
        var baseUrl = AppConstants.ApiBaseUrl.TrimEnd('/');
        return baseUrl + (url.StartsWith('/') ? url : "/" + url);
    }
}

public class StoreDetailDto
{
    public Guid   Id                     { get; set; }
    public string Name                   { get; set; } = "";
    public string Description            { get; set; } = "";
    public string Phone                  { get; set; } = "";
    public string Email                  { get; set; } = "";
    public string Address                { get; set; } = "";
    public string Area                   { get; set; } = "";
    public string City                   { get; set; } = "";
    public string State                  { get; set; } = "";
    public decimal AverageRating         { get; set; }
    public int    TotalReviews           { get; set; }
    public int    TotalOrders            { get; set; }
    public decimal PickupFee             { get; set; }
    public decimal DeliveryFee           { get; set; }
    public int    EstimatedPickupMinutes { get; set; }
    public bool   IsVerified             { get; set; }
    public bool   IsTopRated             { get; set; }
    public bool   FastPickup             { get; set; }
    public string? OpeningHours          { get; set; }
    public string? LogoUrl               { get; set; }
    public string? BannerUrl             { get; set; }
    public string Status                 { get; set; } = "";
    public DateTime CreatedAt            { get; set; }
    public List<ServiceSummaryDto> Services     { get; set; } = new();
    public List<ReviewDto>         RecentReviews{ get; set; } = new();
    public bool   IsCurrentlyOpen { get; set; } = true;

    // â”€â”€ Display helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public string BadgeText =>
        IsVerified && IsTopRated ? "Verified · Top Rated"
        : IsVerified             ? "Verified"
        : IsTopRated             ? "Top Rated"
        : FastPickup             ? "Fast Pickup"
        : "";

    public string RatingDisplay        => TotalReviews > 0
        ? $"Rating {AverageRating:F1} ({TotalReviews} review{(TotalReviews == 1 ? "" : "s")})"
        : "No reviews yet";
    public string PickupFeeDisplay     => $"₦{PickupFee:N0}";
    public string DeliveryFeeDisplay   => $"₦{DeliveryFee:N0}";
    public string EstimatedTimeDisplay => $"~{EstimatedPickupMinutes} min pickup";

    public bool   IsOpen         => Status == "Active" && IsCurrentlyOpen;
    public string OpenStatusText => IsOpen ? "Open" : "Closed";

    // â”€â”€ Image helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public string? FullLogoUrl   => ResolveImageUrl(LogoUrl);
    public string? FullBannerUrl => ResolveImageUrl(BannerUrl);

    /// Best available image: prefer banner, fall back to logo.
    public string? BestImageUrl  => FullBannerUrl ?? FullLogoUrl;
    public bool    HasImage      => !string.IsNullOrEmpty(BestImageUrl);

    public string NameInitial => Name.Length > 0
        ? Name.Substring(0, 1).ToUpperInvariant()
        : "?";

    private static string? ResolveImageUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            // Android emulator: the API may return localhost URLs â€” rewrite to API host
            var apiHost = new Uri(AppConstants.ApiBaseUrl).Host;
            url = System.Text.RegularExpressions.Regex.Replace(
                url, @"://localhost(:\d+)?", $"://{apiHost}$1",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return url;
        }
        var baseUrl = AppConstants.ApiBaseUrl.TrimEnd('/');
        return baseUrl + (url.StartsWith('/') ? url : "/" + url);
    }
}

public class ServiceSummaryDto
{
    public Guid    Id             { get; set; }
    public string  ServiceType   { get; set; } = "";
    public string  Name          { get; set; } = "";
    public string? Description   { get; set; }
    public string  PricingMode   { get; set; } = "";
    public decimal BasePrice     { get; set; }
    public decimal MinimumCharge { get; set; }
    public bool    IsActive      { get; set; }
    public int     EstimatedHours{ get; set; }

    public string PriceDisplay => PricingMode switch
    {
        "PerKg" => $"₦{BasePrice:N0}/kg",
        "PerItem" => $"₦{BasePrice:N0}/item",
        "Fixed" => $"₦{BasePrice:N0} fixed",
        _ => $"₦{BasePrice:N0}"
    };
    public string MinChargeDisplay => MinimumCharge > 0 ? $"Min ₦{MinimumCharge:N0}" : "";
    public string TimeDisplay      => EstimatedHours > 0 ? $"~{EstimatedHours}h" : "";
}

// â”€â”€â”€ ORDER â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
public record PriceEstimateRequest(Guid ServiceOfferingId, decimal Weight, int Pieces);

/// <summary>One line item when placing a multi-service order.</summary>
public class CreateOrderItemRequest
{
    public Guid   ServiceOfferingId { get; set; }
    public decimal Weight           { get; set; }
    public int    Pieces            { get; set; }
}

public class PriceEstimateResponse
{
    public decimal LaundryCost          { get; set; }
    public decimal PickupFee            { get; set; }
    public decimal DeliveryFee          { get; set; }
    public decimal TotalEstimate        { get; set; }
    public decimal PickupFeePayNow      { get; set; }
    public decimal BalanceDueOnDelivery { get; set; }
    public string  Breakdown            { get; set; } = "";
}

public class CreateOrderRequest
{
    public Guid     StoreId              { get; set; }
    public Guid     ServiceOfferingId   { get; set; }   // primary / fallback service
    public decimal  EstimatedWeight     { get; set; }
    public int      EstimatedPieces     { get; set; }
    public string?  SpecialInstructions { get; set; }
    public DateTime RequestedPickupDate { get; set; }
    public string   RequestedPickupSlot { get; set; } = "Morning";
    public string   PickupAddress       { get; set; } = "";
    public string   DeliveryAddress     { get; set; } = "";
    public string   PaymentMethod       { get; set; } = "CashOnDelivery";
    /// <summary>Multi-service line items. When present the API stores each item.</summary>
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class OrderDto
{
    public Guid   Id              { get; set; }
    public string OrderNumber     { get; set; } = "";
    public CustomerSummaryDto Customer { get; set; } = new();
    public StoreSummaryDto    Store    { get; set; } = new();
    public ServiceSummaryDto  Service  { get; set; } = new();
    public RiderSummaryDto?   Rider         { get; set; }
    public RiderSummaryDto?   DeliveryRider { get; set; }
    public string Status              { get; set; } = "";
    public string StatusName          { get; set; } = "";
    public string PaymentState        { get; set; } = "";
    public string PaymentStateName    { get; set; } = "";
    public string PaymentMethod       { get; set; } = "";
    public decimal EstimatedWeight    { get; set; }
    public int    EstimatedPieces     { get; set; }
    public decimal EstimatedLaundryCost{ get; set; }
    public decimal ActualLaundryCost  { get; set; }
    public decimal PickupFee          { get; set; }
    public decimal DeliveryFee        { get; set; }
    public decimal TotalEstimate      { get; set; }
    public decimal ActualTotal        { get; set; }
    public decimal PickupFeeAmount    { get; set; }
    public decimal BalanceAmount      { get; set; }
    public bool   AdminPriceOverride  { get; set; }
    public string? AdminOverrideReason{ get; set; }
    public string PickupAddress       { get; set; } = "";
    public string DeliveryAddress     { get; set; } = "";
    public DateTime RequestedPickupDate{ get; set; }
    public string RequestedPickupSlot { get; set; } = "";
    public string? SpecialInstructions{ get; set; }
    public DateTime? PickedUpAt            { get; set; }
    public DateTime? ProcessingStartedAt   { get; set; }
    public DateTime? ReadyAt               { get; set; }
    public DateTime? OutForDeliveryAt      { get; set; }
    public DateTime? DeliveredAt           { get; set; }
    public DateTime? CompletedAt           { get; set; }
    public DateTime  CreatedAt             { get; set; }
    public List<StatusHistoryDto>  StatusHistory { get; set; } = new();
    public List<OrderItemDto>      Items         { get; set; } = new();
    public ReviewDto?        Review  { get; set; }
    public DisputeSummaryDto? Dispute { get; set; }

    public bool CanPayPickupFee => PaymentState == "Unpaid"         && Status == "Pending";
    public bool CanPayBalance   => Status == "Delivered"            && PaymentState == "PickupFeePaid";
    // Allow review after any delivery completion â€” Delivered, BalancePaid, or Completed
    public bool CanReview       => (Status == "Completed" || Status == "BalancePaid" || Status == "Delivered")
                                   && Review == null;
    public bool CanDispute      => Dispute == null
        && Status is not ("Cancelled" or "Pending");

    public string StatusEmoji => "•";

    /// Friendly one-line description of the current status shown on the Track screen.
    public string StatusDescription => Status switch
    {
        "Pending"        => "Awaiting pickup fee payment",
        "Confirmed"      => "Order confirmed",
        "PickupFeePaid"  => "Pickup fee paid - rider being assigned",
        "RiderAssigned"  => "Rider assigned",
        "PickedUp"       => "Laundry picked up",
        "Processing"     => "Being cleaned & processed",
        "Ready"          => "Ready for delivery",
        "OutForDelivery" => "On the way to you",
        "Delivered"      => "Delivered - please pay balance",
        "BalancePaid"    => "Balance paid",
        "Completed"      => "Order complete!",
        "Disputed"       => "Dispute raised",
        "Cancelled"      => "Order cancelled",
        _                => StatusName
    };

    /// Short secondary hint shown below the description on the Track screen.
    public string StatusSubtitle => Status switch
    {
        "Pending"        => "Tap 'Pay Pickup Fee' to get started",
        "PickupFeePaid"  => "We'll notify you when a rider is assigned",
        "RiderAssigned"  => "Your rider is heading to your address",
        "PickedUp"       => "Your items are at the laundry store",
        "Processing"     => $"Est. {Service.TimeDisplay} turnaround",
        "Ready"          => "Rider will be dispatched shortly",
        "OutForDelivery" => "Expected soon - stay close!",
        "Delivered"      => $"Balance due: ₦{BalanceAmount:N0}",
        "Completed"      => "Thank you for using VYRON!",
        _                => ""
    };
}

public class CustomerSummaryDto
{
    public Guid   Id       { get; set; }
    public string FullName { get; set; } = "";
    public string Phone    { get; set; } = "";
}

public class StoreSummaryDto
{
    public Guid    Id            { get; set; }
    public string  Name          { get; set; } = "";
    public string  Address       { get; set; } = "";
    public decimal AverageRating { get; set; }
    public string? LogoUrl       { get; set; }
    public string  Phone         { get; set; } = "";
}

public class RiderSummaryDto
{
    public Guid   Id           { get; set; }
    public string FullName     { get; set; } = "";
    public string Phone        { get; set; } = "";
    public string VehicleType  { get; set; } = "";
    public string? VehiclePlate{ get; set; }
}

public class StatusHistoryDto
{
    public string   Status     { get; set; } = "";
    public string   StatusName { get; set; } = "";
    public string?  Note       { get; set; }
    public DateTime ChangedAt  { get; set; }
}

public class OrderItemDto
{
    public Guid    Id                 { get; set; }
    public Guid    ServiceOfferingId  { get; set; }
    public string  ServiceName        { get; set; } = "";
    public string  PricingMode        { get; set; } = "PerKg";
    public decimal Weight             { get; set; }
    public int     Pieces             { get; set; }
    public decimal UnitPrice          { get; set; }
    public decimal LineTotal          { get; set; }
    public string LineTotalDisplay => $"₦{LineTotal:N0}";
    public string QtyDisplay => PricingMode switch
    {
        "PerKg" => $"{Weight:F1} kg",
        "PerItem" => $"{Pieces} pcs",
        "Fixed" => "Fixed",
        _ => Pieces > 0 ? $"{Pieces} pcs" : $"{Weight:F1} kg"
    };
}

// â”€â”€â”€ REVIEW â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
public record CreateReviewRequest(Guid OrderId, int Rating, string? Comment);

public class ReviewDto
{
    public Guid   Id           { get; set; }
    public Guid   OrderId      { get; set; }
    public string OrderNumber  { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public int    Rating       { get; set; }
    public string? Comment     { get; set; }
    public string? PhotoUrl    { get; set; }
    public bool   IsVisible    { get; set; }
    public DateTime CreatedAt  { get; set; }

    public string StarsDisplay => new string('★', Rating) + new string('☆', 5 - Rating);
}

// â”€â”€â”€ DISPUTE â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
public record CreateDisputeRequest(Guid OrderId, string Type, string Description, string? EvidenceUrl);

public class DisputeSummaryDto
{
    public Guid   Id        { get; set; }
    public string Type      { get; set; } = "";
    public string Status    { get; set; } = "";
    public DateTime CreatedAt{ get; set; }
}

public class DisputeDetailDto
{
    public Guid   Id             { get; set; }
    public Guid   OrderId        { get; set; }
    public string OrderNumber    { get; set; } = "";
    public string RaisedByName   { get; set; } = "";
    public string Type           { get; set; } = "";
    public string TypeName       { get; set; } = "";
    public string Status         { get; set; } = "";
    public string StatusName     { get; set; } = "";
    public string Description    { get; set; } = "";
    public string? EvidenceUrl   { get; set; }
    public string? Resolution    { get; set; }
    public string? ResolutionNote{ get; set; }
    public decimal? RefundAmount { get; set; }
    public DateTime  CreatedAt   { get; set; }
    public DateTime? ResolvedAt  { get; set; }
    public List<DisputeMessageDto> Messages { get; set; } = new();
}

public class DisputeMessageDto
{
    public Guid   Id             { get; set; }
    public string SenderName     { get; set; } = "";
    public bool   IsAdminMessage { get; set; }
    public string Message        { get; set; } = "";
    public DateTime SentAt       { get; set; }
}

// â”€â”€â”€ PAYMENT â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
public class RecordPaymentRequest
{
    public Guid   OrderId    { get; set; }
    public decimal Amount    { get; set; }
    public string Method     { get; set; } = "CashOnDelivery";
    public string Type       { get; set; } = "PickupFee";
    public string? GatewayRef{ get; set; }
    public string? Notes     { get; set; }
}

public class PaymentDto
{
    public Guid   Id          { get; set; }
    public string PaymentRef  { get; set; } = "";
    public decimal Amount     { get; set; }
    public string Method      { get; set; } = "";
    public string Type        { get; set; } = "";
    public bool   IsSuccessful{ get; set; }
    public string? GatewayRef { get; set; }
    public DateTime CreatedAt { get; set; }
}

// â”€â”€â”€ PAGED RESULT â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
public class PagedResult<T>
{
    public List<T> Items      { get; set; } = new();
    public int     TotalCount { get; set; }
    public int     Page       { get; set; }
    public int     PageSize   { get; set; }
    public bool    HasMore    => Items.Count == PageSize;
}

// â”€â”€â”€ NOTIFICATION â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
public class NotificationDto
{
    public Guid     Id        { get; set; }
    public string   Title     { get; set; } = "";
    public string   Message   { get; set; } = "";
    public string   Type      { get; set; } = "";
    public bool     IsRead    { get; set; }
    public DateTime CreatedAt { get; set; }

    public string TimeDisplay => CreatedAt.ToLocalTime() switch
    {
        var t when t.Date == DateTime.Today             => $"Today {t:h:mm tt}",
        var t when t.Date == DateTime.Today.AddDays(-1) => $"Yesterday {t:h:mm tt}",
        var t                                           => t.ToString("d MMM, h:mm tt")
    };
}

// â”€â”€â”€ RIDER MESSAGE â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
public record SendRiderMessageRequest(string Message);
