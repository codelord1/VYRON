using Vyron.Shared.Enums;

namespace Vyron.API.Models;

// ─── USER ─────────────────────────────────────────────────────────
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public bool IsActive { get; set; } = true;
    public string? ProfilePhoto { get; set; }
    public string? ProfileImageFileName { get; set; }
    public string? ProfileImageContentType { get; set; }
    public long ProfileImageSize { get; set; }
    public DateTime? ProfileImageUpdatedAt { get; set; }
    public string CountryCode { get; set; } = "+234";
    public string? LocalPhone { get; set; }
    public bool PhoneNumberVerified { get; set; } = false;
    public DateTime? PasswordChangedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Dispute> Disputes { get; set; } = new List<Dispute>();
    // Role table relationships (UserRoles is authoritative; Role column kept for backward compat)
    public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
}

// ─── LAUNDRY STORE ────────────────────────────────────────────────
public class LaundryStore
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string City { get; set; } = "Lagos";
    public string State { get; set; } = "Lagos";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? LogoUrl { get; set; }
    public string? BannerUrl { get; set; }
    public StoreStatus Status { get; set; } = StoreStatus.Pending;
    public bool IsVerified { get; set; }
    public bool IsTopRated { get; set; }
    public bool FastPickup { get; set; }
    public int EstimatedPickupMinutes { get; set; } = 30;
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public int TotalOrders { get; set; }
    public decimal PickupFee { get; set; } = 1000;
    public decimal DeliveryFee { get; set; } = 1000;
    public string? OpeningHours { get; set; } = "8:00 AM - 8:00 PM";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User Owner { get; set; } = null!;
    public ICollection<ServiceOffering> Services { get; set; } = new List<ServiceOffering>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<StoreImage> StoreImages { get; set; } = new List<StoreImage>();
}

// ─── SERVICE TYPE (admin-managed catalog) ─────────────────────────
/// <summary>
/// Admin-managed service type catalog, replacing the hard-coded ServiceType enum
/// for new service offerings.  Existing ServiceOffering.ServiceType (enum) column
/// is kept for backward compatibility with seeded data.
/// </summary>
public class ServiceTypeEntity
{
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public string Name        { get; set; } = string.Empty;   // e.g. "Wash Only"
    public string? Description{ get; set; }
    public int    SortOrder   { get; set; }
    public bool   IsActive    { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ServiceOffering> ServiceOfferings { get; set; } = new List<ServiceOffering>();
}

// ─── SERVICE OFFERING ─────────────────────────────────────────────
public class ServiceOffering
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public ServiceType ServiceType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PricingMode PricingMode { get; set; } = PricingMode.PerKg;
    public decimal BasePrice { get; set; }
    public decimal MinimumCharge { get; set; }
    public bool IsActive { get; set; } = true;
    public int EstimatedHours { get; set; } = 24;
    public string? Category { get; set; }
    /// <summary>
    /// Optional FK to admin-managed ServiceTypeEntity.
    /// Null for seeded/legacy records (which use the ServiceType enum column).
    /// </summary>
    public Guid? ServiceTypeEntityId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public LaundryStore Store { get; set; } = null!;
    public ServiceTypeEntity? ServiceTypeRef { get; set; }
}

// ─── ORDER ────────────────────────────────────────────────────────
public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid StoreId { get; set; }
    public Guid ServiceOfferingId { get; set; }
    public Guid? RiderId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentState PaymentState { get; set; } = PaymentState.Unpaid;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOnDelivery;
    public decimal EstimatedWeight { get; set; }
    public int EstimatedPieces { get; set; }
    public decimal EstimatedLaundryCost { get; set; }
    public decimal ActualLaundryCost { get; set; }
    public decimal PickupFee { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TotalEstimate { get; set; }
    public decimal ActualTotal { get; set; }
    public decimal PickupFeeAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public bool AdminPriceOverride { get; set; }
    public string? AdminOverrideReason { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public DateTime RequestedPickupDate { get; set; }
    public string RequestedPickupSlot { get; set; } = string.Empty;
    public string? SpecialInstructions { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? PickupFeePaidAt { get; set; }
    public DateTime? RiderAssignedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? OutForDeliveryAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? BalancePaidAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User Customer { get; set; } = null!;
    public LaundryStore Store { get; set; } = null!;
    public ServiceOffering Service { get; set; } = null!;
    public Rider? Rider { get; set; }
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public Review? Review { get; set; }
    public Dispute? Dispute { get; set; }
}

// ─── ORDER ITEM ────────────────────────────────────────────────────
public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid ServiceOfferingId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string PricingMode { get; set; } = "PerKg";   // "PerKg" | "PerPiece"
    public decimal Weight { get; set; }
    public int Pieces { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Order Order { get; set; } = null!;
    public ServiceOffering ServiceOffering { get; set; } = null!;
}

// ─── ORDER STATUS HISTORY ─────────────────────────────────────────
public class OrderStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public OrderStatus Status { get; set; }
    public string? Note { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public Order Order { get; set; } = null!;
}

// ─── RIDER ────────────────────────────────────────────────────────
public class Rider
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string VehicleType { get; set; } = "Motorcycle";
    public string? VehiclePlate { get; set; }
    public RiderStatus Status { get; set; } = RiderStatus.Offline;
    public double CurrentLatitude { get; set; }
    public double CurrentLongitude { get; set; }
    public int TotalDeliveries { get; set; }
    public decimal TotalEarnings { get; set; }
    public bool IsApproved { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByAdminId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

// ─── REVIEW ───────────────────────────────────────────────────────
public class Review
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid StoreId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsFlagged { get; set; }
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Order Order { get; set; } = null!;
    public User Customer { get; set; } = null!;
    public LaundryStore Store { get; set; } = null!;
}

// ─── DISPUTE ──────────────────────────────────────────────────────
public class Dispute
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid RaisedByUserId { get; set; }
    public DisputeType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? EvidenceUrl { get; set; }
    public DisputeStatus Status { get; set; } = DisputeStatus.Open;
    public DisputeResolution? Resolution { get; set; }
    public string? AdminNotes { get; set; }
    public string? ResolutionNote { get; set; }
    public decimal? RefundAmount { get; set; }
    public Guid? AssignedAdminId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    public Order Order { get; set; } = null!;
    public User RaisedBy { get; set; } = null!;
    public ICollection<DisputeMessage> Messages { get; set; } = new List<DisputeMessage>();
}

public class DisputeMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DisputeId { get; set; }
    public Guid SenderId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsAdminMessage { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public Dispute Dispute { get; set; } = null!;
    public User Sender { get; set; } = null!;
}

// ─── PAYMENT ──────────────────────────────────────────────────────
public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string PaymentRef { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string Type { get; set; } = "pickup_fee";
    public bool IsSuccessful { get; set; }
    public string? GatewayRef { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Order Order { get; set; } = null!;
}

// ─── ADDRESS ──────────────────────────────────────────────────────
public class Address
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Label { get; set; } = "Home";
    public string Street { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string City { get; set; } = "Lagos";
    public string State { get; set; } = "Lagos";
    public string? Landmark { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}

// ─── OTP / AUTH ───────────────────────────────────────────────────
public class OtpCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Phone { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public int AttemptCount { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}

// ─── SYSTEM CONFIG ────────────────────────────────────────────────
public class SystemConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByUserId { get; set; }
}

// ─── AUDIT LOG ────────────────────────────────────────────────────
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User? User { get; set; }
}

// ─── STORE IMAGE ──────────────────────────────────────────────────
public class StoreImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool IsPrimary { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public LaundryStore Store { get; set; } = null!;
}

// ─── PASSWORD RESET TOKEN ─────────────────────────────────────────
public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}

// ─── NOTIFICATION ─────────────────────────────────────────────────
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "sms";
    public bool IsSent { get; set; }
    public bool IsRead { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User? User { get; set; }
}

// ─── ROLE (authoritative role definitions) ────────────────────────
/// <summary>
/// Platform roles table.  UserRole enum remains the fast read;
/// this table is the authoritative source for JWT claim generation.
/// Table name: Roles
/// </summary>
public class AppRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;          // e.g. "Admin"
    public string NormalizedName { get; set; } = string.Empty; // e.g. "ADMIN"
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
}

// ─── COMMUNICATION LOG ────────────────────────────────────────────
/// <summary>
/// Audit trail for all outbound communications (SMS, Email, InApp messages).
/// Every send attempt — successful or not — is logged here.
/// </summary>
public class CommunicationLog
{
    public Guid    Id                  { get; set; } = Guid.NewGuid();
    /// <summary>InApp | SMS | Email</summary>
    public string  Channel             { get; set; } = "InApp";
    /// <summary>Pending | Sent | Failed</summary>
    public string  Status              { get; set; } = "Pending";
    public Guid?   RecipientUserId     { get; set; }
    public string? RecipientName       { get; set; }
    public string? RecipientPhone      { get; set; }
    public string? RecipientEmail      { get; set; }
    public string  Subject             { get; set; } = string.Empty;
    public string  Body                { get; set; } = string.Empty;
    /// <summary>Optional context: "Order" | "Dispute" | "Store" | null</summary>
    public string? RelatedEntityType   { get; set; }
    public Guid?   RelatedEntityId     { get; set; }
    public Guid?   SentByAdminId       { get; set; }
    public string? ErrorMessage        { get; set; }
    public DateTime? SentAt            { get; set; }
    public DateTime  CreatedAt         { get; set; } = DateTime.UtcNow;

    public User? Recipient    { get; set; }
    public User? SentByAdmin  { get; set; }
}

// ─── USER-ROLE MAPPING ─────────────────────────────────────────────
/// <summary>
/// Many-to-many join table between Users and Roles.
/// Unique index on (UserId, RoleId).
/// Table name: UserRoles
/// </summary>
public class AppUserRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }

    public User User { get; set; } = null!;
    public AppRole Role { get; set; } = null!;
}
