using Vyron.Shared.Enums;

namespace Vyron.API.DTOs;

// ─── AUTH ─────────────────────────────────────────────────────────
public record SendOtpRequest(string Phone);

// ─── CUSTOMER AUTH (password-based login + OTP signup/reset) ─────
public record CustomerLoginRequest(string Phone, string Password);
public record CustomerLoginResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserDto User);
public record SignupOtpRequest(string Phone);
public record VerifySignupOtpRequest(string Phone, string Code);
public record VerifySignupOtpResponse(bool PhoneVerified, string VerificationToken);
public record CompleteProfileRequest(string Phone, string Code, string FullName, string? Email, string Password);
public record PasswordResetOtpRequest(string Phone);
public record ResetPasswordRequest(string Phone, string Code, string NewPassword);
public record VerifyOtpRequest(string Phone, string Code, string? FullName);
public record RefreshTokenRequest(string RefreshToken);
public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserDto User);

/// <summary>
/// Extended response from POST /api/auth/verify-otp.
/// IsNewUser is true on the phone's very first OTP verification.
/// RequiresProfileCompletion is true when FullName has not been set yet —
/// the customer app should route to the CompleteProfile screen.
/// Admin/Rider apps receiving this response can safely ignore the extra fields.
/// </summary>
public record VerifyOtpResponse(
    string AccessToken, string RefreshToken, DateTime ExpiresAt,
    UserDto User, bool IsNewUser, bool RequiresProfileCompletion);

/// <summary>
/// Returned by POST /api/auth/send-otp.
/// DevOtp is ONLY populated in the Development environment (controlled by Otp:ReturnOtpInDevelopment).
/// It is null in Production — never expose OTPs in production.
/// </summary>
public record SendOtpResponse(bool Success, string Message, string? DevOtp = null);

public record UserDto(Guid Id, string Phone, string FullName, string? Email, UserRole Role, DateTime CreatedAt);

// ─── STORE ────────────────────────────────────────────────────────
public record StoreListItemDto(
    Guid Id, string Name, string Description, string Address, string Area,
    decimal AverageRating, int TotalReviews, int TotalOrders,
    decimal PickupFee, decimal DeliveryFee, int EstimatedPickupMinutes,
    bool IsVerified, bool IsTopRated, bool FastPickup,
    StoreStatus Status, string? LogoUrl,
    double Latitude, double Longitude,
    List<ServiceSummaryDto> Services,
    bool IsCurrentlyOpen = true);

public record StoreDetailDto(
    Guid Id, string Name, string Description, string Phone, string Email,
    string Address, string Area, string City, string State,
    decimal AverageRating, int TotalReviews, int TotalOrders,
    decimal PickupFee, decimal DeliveryFee, int EstimatedPickupMinutes,
    bool IsVerified, bool IsTopRated, bool FastPickup,
    string? OpeningHours, string? LogoUrl, string? BannerUrl,
    StoreStatus Status, DateTime CreatedAt,
    List<ServiceSummaryDto> Services,
    List<ReviewDto> RecentReviews,
    bool IsCurrentlyOpen = true);

public record ServiceSummaryDto(
    Guid Id, ServiceType ServiceType, string Name, string? Description,
    PricingMode PricingMode, decimal BasePrice, decimal MinimumCharge,
    bool IsActive, int EstimatedHours);

public record CreateStoreRequest(
    string Name, string Description, string Phone, string Email,
    string Address, string Area, string City, string State,
    double Latitude, double Longitude, decimal PickupFee, decimal DeliveryFee,
    int EstimatedPickupMinutes, string? OpeningHours);

public record UpdateStoreRequest(
    string Name, string Description, string Phone, string Email,
    string Address, string Area, string City, string State,
    double Latitude, double Longitude, decimal PickupFee, decimal DeliveryFee,
    int EstimatedPickupMinutes, string? OpeningHours);

public record UpsertServiceRequest(
    ServiceType ServiceType, string Name, string? Description,
    PricingMode PricingMode, decimal BasePrice, decimal MinimumCharge,
    bool IsActive, int EstimatedHours);

// ─── ORDER ────────────────────────────────────────────────────────
/// <summary>One line item in a multi-service order.</summary>
public record CreateOrderItemRequest(
    Guid ServiceOfferingId,
    decimal Weight,
    int Pieces);

public record CreateOrderRequest(
    Guid StoreId, Guid ServiceOfferingId,
    decimal EstimatedWeight, int EstimatedPieces,
    string? SpecialInstructions,
    DateTime RequestedPickupDate, string RequestedPickupSlot,
    string PickupAddress, string DeliveryAddress,
    PaymentMethod PaymentMethod = PaymentMethod.CashOnDelivery,
    List<CreateOrderItemRequest>? Items = null);

public record PriceEstimateRequest(Guid ServiceOfferingId, decimal Weight, int Pieces);

public record PriceEstimateResponse(
    decimal LaundryCost, decimal PickupFee, decimal DeliveryFee, decimal TotalEstimate,
    decimal PickupFeePayNow, decimal BalanceDueOnDelivery, string Breakdown);

public record UpdateOrderStatusRequest(OrderStatus Status, string? Note);
public record AssignRiderRequest(Guid RiderId);
public record AssignDeliveryRiderRequest(Guid RiderId);
public record OverridePriceRequest(decimal ActualLaundryCost, string Reason);

public record OrderDto(
    Guid Id, string OrderNumber,
    CustomerSummaryDto Customer,
    StoreSummaryDto Store,
    ServiceSummaryDto Service,
    RiderSummaryDto? Rider,
    OrderStatus Status, string StatusName,
    PaymentState PaymentState, string PaymentStateName,
    PaymentMethod PaymentMethod,
    decimal EstimatedWeight, int EstimatedPieces,
    decimal EstimatedLaundryCost, decimal ActualLaundryCost,
    decimal PickupFee, decimal DeliveryFee,
    decimal TotalEstimate, decimal ActualTotal,
    decimal PickupFeeAmount, decimal BalanceAmount,
    bool AdminPriceOverride, string? AdminOverrideReason,
    string PickupAddress, string DeliveryAddress,
    DateTime RequestedPickupDate, string RequestedPickupSlot,
    string? SpecialInstructions,
    DateTime? PickedUpAt, DateTime? ProcessingStartedAt,
    DateTime? ReadyAt, DateTime? OutForDeliveryAt,
    DateTime? DeliveredAt, DateTime? CompletedAt,
    DateTime CreatedAt,
    List<StatusHistoryDto> StatusHistory,
    ReviewDto? Review, DisputeSummaryDto? Dispute,
    RiderSummaryDto? DeliveryRider = null,
    List<OrderItemDto>? Items = null);

public record CustomerSummaryDto(Guid Id, string FullName, string Phone);
public record StoreSummaryDto(Guid Id, string Name, string Address, decimal AverageRating, string? LogoUrl, string Phone = "");
public record RiderSummaryDto(Guid Id, string FullName, string Phone, string VehicleType, string? VehiclePlate);
public record StatusHistoryDto(OrderStatus Status, string StatusName, string? Note, DateTime ChangedAt);
public record OrderItemDto(
    Guid Id, Guid ServiceOfferingId, string ServiceName,
    string PricingMode, decimal Weight, int Pieces,
    decimal UnitPrice, decimal LineTotal);

// ─── REVIEW ───────────────────────────────────────────────────────
public record CreateReviewRequest(Guid OrderId, int Rating, string? Comment, string? PhotoUrl);
public record ReviewDto(
    Guid Id, Guid OrderId, string OrderNumber,
    string CustomerName, int Rating, string? Comment,
    string? PhotoUrl, bool IsVisible, DateTime CreatedAt);

// ─── DISPUTE ──────────────────────────────────────────────────────
public record CreateDisputeRequest(Guid OrderId, DisputeType Type, string Description, string? EvidenceUrl);
public record ResolveDisputeRequest(DisputeResolution Resolution, string ResolutionNote, decimal? RefundAmount);
public record AddDisputeMessageRequest(string Message);

public record DisputeSummaryDto(Guid Id, DisputeType Type, DisputeStatus Status, DateTime CreatedAt);
public record DisputeDetailDto(
    Guid Id, Guid OrderId, string OrderNumber,
    string RaisedByName, string RaisedByPhone,
    DisputeType Type, string TypeName,
    DisputeStatus Status, string StatusName,
    string Description, string? EvidenceUrl,
    DisputeResolution? Resolution, string? ResolutionNote,
    decimal? RefundAmount, string? AdminNotes,
    DateTime CreatedAt, DateTime? ResolvedAt,
    List<DisputeMessageDto> Messages);
public record DisputeMessageDto(Guid Id, string SenderName, bool IsAdminMessage, string Message, DateTime SentAt);

// ─── PAYMENT ──────────────────────────────────────────────────────
public record RecordPaymentRequest(
    Guid OrderId, decimal Amount, PaymentMethod Method,
    string Type, string? GatewayRef, string? Notes);

public record PaymentDto(
    Guid Id, string PaymentRef, decimal Amount,
    PaymentMethod Method, string Type,
    bool IsSuccessful, string? GatewayRef, DateTime CreatedAt);

// ─── RIDER ────────────────────────────────────────────────────────
public record CreateRiderRequest(string Phone, string FullName, string VehicleType, string? VehiclePlate);

public record RiderDto(
    Guid Id, Guid UserId, string FullName, string Phone,
    string VehicleType, string? VehiclePlate,
    RiderStatus Status, double CurrentLatitude, double CurrentLongitude,
    int TotalDeliveries, decimal TotalEarnings, int ActiveOrderCount);

// ─── ANALYTICS ────────────────────────────────────────────────────
public record DashboardSummaryDto(
    int OrdersToday, int PendingPickups, int InProcessing,
    int OutForDelivery, int ActiveDisputes,
    decimal RevenueToday, decimal RevenueWeek, decimal RevenueMonth,
    int TotalStores, int ActiveRiders, int TotalCustomers, int CompletedOrders,
    List<DailyMetric> DailyOrdersWeek,
    List<StatusCount> OrdersByStatus);
public record DailyMetric(string Day, int Orders, decimal Revenue);
public record StatusCount(string Status, int Count);

// ─── NOTIFICATION ─────────────────────────────────────────────────
public record NotificationDto(
    Guid Id, string Title, string Message, string Type,
    bool IsRead, DateTime CreatedAt);

// ─── RIDER MESSAGE ────────────────────────────────────────────────
public record SendRiderMessageRequest(string Message);

// ─── SYSTEM CONFIG ────────────────────────────────────────────────
public record UpdateConfigRequest(string Value);
public record ConfigDto(Guid Id, string Key, string Value, string? Description, DateTime UpdatedAt);

// ─── PROFILE ──────────────────────────────────────────────────────
public record ProfileDto(Guid Id, string FullName, string Phone, string? Email,
    UserRole Role, string? ProfilePhoto, DateTime CreatedAt);
public record UpdateProfileRequest(string? FullName, string? Email);

// ─── ADDRESS ──────────────────────────────────────────────────────
public record CreateAddressRequest(
    string Label, string Street, string Area,
    string City, string State, string? Landmark,
    double Latitude, double Longitude, bool IsDefault);
public record AddressDto(
    Guid Id, string Label, string Street, string Area,
    string City, string State, string? Landmark,
    double Latitude, double Longitude, bool IsDefault);
