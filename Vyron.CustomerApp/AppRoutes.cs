namespace Vyron.CustomerApp;

/// <summary>
/// Single source of truth for all Shell navigation route strings.
/// Use these constants everywhere instead of raw string literals.
///
/// Absolute tab routes (//main/&lt;tab&gt;):
///   AppShell TabBar Route="main", each Tab has its own Route.
///   Full URI = //main/{tab-route}
/// </summary>
public static class AppRoutes
{
    //// ── Root absolute routes ────────────────────────────────────────
    ///// <summary>Login screen (no tab bar).</summary>
    //public const string Login = "//login";

    //// ── Main tab absolute routes ────────────────────────────────────
    ///// <summary>Stores browse/search tab root.</summary>
    //public const string Stores = "//main/stores";

    ///// <summary>Order history tab root.</summary>
    //public const string Orders = "//main/orders";

    ///// <summary>Active order tracker tab root.</summary>
    //public const string Track = "//main/track";

    ///// <summary>Account/more tab root.</summary>
    //public const string More = "//main/more";

    public const string Login = "//login";
    public const string Stores = "//main/storesTab/stores";
    public const string Orders = "//main/ordersTab/orders";
    public const string Track = "//main/trackTab/track";
    public const string More = "//main/moreTab/more";

    // ── Named (relative push) routes ───────────────────────────────
    public const string StoreDetails      = "storeDetails";
    public const string ServiceSelection  = "serviceSelection";
    public const string CreateOrder       = "createOrder";
    public const string OrderSuccess      = "orderSuccess";
    public const string OrderDetails      = "orderDetails";
    public const string PickupFeePayment  = "pickupFeePayment";
    public const string BalancePayment    = "balancePayment";
    public const string RaiseDispute      = "raiseDispute";
    public const string DisputeHistory    = "disputeHistory";
    public const string AddReview         = "addReview";
    public const string Profile           = "profile";
    public const string Notifications     = "notifications";
    public const string MessageRider      = "messageRider";
    public const string Signup            = "signup";
    public const string VerifyOtp         = "verifyOtp";
    public const string CompleteProfile   = "completeProfile";
    public const string ForgotPassword    = "forgotPassword";
    public const string ResetPassword     = "resetPassword";
}
