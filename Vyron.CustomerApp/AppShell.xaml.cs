using Vyron.CustomerApp.Views.Auth;
using Vyron.CustomerApp.Views.Disputes;
using Vyron.CustomerApp.Views.More;
using Vyron.CustomerApp.Views.Orders;
using Vyron.CustomerApp.Views.Profile;
using Vyron.CustomerApp.Views.Reviews;
using Vyron.CustomerApp.Views.Stores;
using Vyron.CustomerApp.Views;

namespace Vyron.CustomerApp;

public partial class AppShell : Shell
{
    private bool _resettingTab;

    public AppShell()
    {
        InitializeComponent();
        RegisterRoutes();

        // ── Stores-tab navigation reset ──────────────────────────────
        // When the user taps the Stores tab while sub-pages (StoreDetails,
        // ServiceSelection, etc.) are on the navigation stack, MAUI Shell does NOT
        // automatically pop them.  We listen to the Navigated event and, whenever
        // the shell lands back on the Stores root due to a tab-change or an
        // explicit Stores tab navigation, pop everything above the root page so
        // the user always sees a clean StoresPage (search/list).
        Navigated += OnShellNavigated;
    }

    private async void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        if (_resettingTab ||
            (e.Source != ShellNavigationSource.ShellSectionChanged &&
             e.Source != ShellNavigationSource.ShellItemChanged))
            return;

        var targetRoute = Current?.CurrentItem?.CurrentItem?.Route switch
        {
            "homeTab" => AppRoutes.Home,
            "storesTab" => AppRoutes.Stores,
            "ordersTab" => AppRoutes.Orders,
            "moreTab" => AppRoutes.More,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(targetRoute) ||
            e.Current.Location.OriginalString == targetRoute)
            return;

        try
        {
            _resettingTab = true;
            await GoToAsync(targetRoute, animate: false);
        }
        finally
        {
            _resettingTab = false;
        }
    }

    private static void RegisterRoutes()
    {
        // Auth routes (no tab bar — pages set Shell.TabBarIsVisible="False" themselves)
        Routing.RegisterRoute("verifyOtp",       typeof(VerifyOtpPage));
        Routing.RegisterRoute("completeProfile", typeof(CompleteProfilePage));
        Routing.RegisterRoute("signup",          typeof(SignupPage));
        Routing.RegisterRoute("forgotPassword",  typeof(ForgotPasswordPage));
        Routing.RegisterRoute("resetPassword",   typeof(ResetPasswordPage));

        // Profile (navigable from More tab)
        Routing.RegisterRoute("profile", typeof(ProfilePage));

        // Store routes
        Routing.RegisterRoute("storeDetails",     typeof(StoreDetailsPage));

        // Order routes
        Routing.RegisterRoute("serviceSelection", typeof(ServiceSelectionPage));
        Routing.RegisterRoute("createOrder",      typeof(CreateOrderPage));
        Routing.RegisterRoute("orderSuccess",     typeof(OrderSuccessPage));
        Routing.RegisterRoute("orderDetails",     typeof(OrderDetailsPage));
        Routing.RegisterRoute("orderTracking",    typeof(TrackPage));
        Routing.RegisterRoute("pickupFeePayment", typeof(PickupFeePaymentPage));
        Routing.RegisterRoute("balancePayment",   typeof(BalancePaymentPage));

        // Dispute & review
        Routing.RegisterRoute("raiseDispute",     typeof(RaiseDisputePage));
        Routing.RegisterRoute("disputeHistory",   typeof(DisputeHistoryPage));
        Routing.RegisterRoute("addReview",        typeof(AddReviewPage));

        // Notifications (More tab)
        Routing.RegisterRoute("notifications",    typeof(NotificationsPage));

        // Message Rider
        Routing.RegisterRoute("messageRider",     typeof(MessageRiderPage));
    }
}
