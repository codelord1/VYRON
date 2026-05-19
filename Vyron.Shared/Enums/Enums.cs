namespace Vyron.Shared.Enums;

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    PickupFeePaid = 2,
    RiderAssigned = 3,
    PickedUp = 4,
    Processing = 5,
    Ready = 6,
    OutForDelivery = 7,
    Delivered = 8,
    BalancePaid = 9,
    Completed = 10,
    Disputed = 11,
    Cancelled = 12
}

public enum PaymentState
{
    Unpaid = 0,
    PickupFeePaid = 1,
    BalancePending = 2,
    FullyPaid = 3,
    Refunded = 4
}

public enum ServiceType
{
    WashOnly = 0,
    WashAndIron = 1,
    IronOnly = 2,
    DryClean = 3,
    WashAndFold = 4,
    SpecialCare = 5
}

public enum PricingMode
{
    PerKg = 0,
    PerItem = 1,
    Fixed = 2
}

public enum RiderAssignmentMode
{
    Manual = 0,
    AutoPlatform = 1,
    AutoThirdParty = 2
}

public enum UserRole
{
    Customer    = 0,
    Rider       = 1,
    StoreOwner  = 2,
    Admin       = 3,
    SuperAdmin  = 4,
    /// <summary>Admin-level user created by SuperAdmin; cannot manage other admins.</summary>
    AdminUser   = 5,
    /// <summary>Store staff manager scoped to one or more stores; created by StoreOwner.</summary>
    StoreManager = 6,
    /// <summary>Store staff scoped to one or more stores; created by StoreOwner or StoreManager.</summary>
    StoreStaff  = 7
}

/// <summary>Approval workflow status for Riders and StoreOwner registrations.</summary>
public enum ApprovalStatus
{
    Pending  = 0,
    Approved = 1,
    Rejected = 2
}

public enum DisputeType
{
    WrongPricing = 0,
    Delay = 1,
    MissingClothes = 2,
    DamagedClothes = 3,
    PoorQuality = 4,
    PaymentIssue = 5,
    Other = 6
}

public enum DisputeStatus
{
    Open = 0,
    UnderReview = 1,
    AwaitingResponse = 2,
    Resolved = 3,
    Refunded = 4,
    Rejected = 5
}

public enum DisputeResolution
{
    Refund = 0,
    PartialRefund = 1,
    Adjustment = 2,
    NoAction = 3
}

public enum StoreStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
    Rejected = 3
}

public enum PaymentMethod
{
    CashOnDelivery = 0,
    BankTransfer = 1,
    Paystack = 2,
    Flutterwave = 3,
    Wallet = 4
}

public enum RiderStatus
{
    Offline = 0,
    OnlineAvailable = 1,   // was Online — renamed for clarity
    OnDelivery = 2,        // legacy compat
    Pending = 3,
    Assigned = 4,
    PickingUp = 5,
    OutForDelivery = 6,
    Suspended = 7,
    Rejected = 8
}

public enum DatabaseProvider
{
    SqlServer = 0,
    PostgreSQL = 1
}
