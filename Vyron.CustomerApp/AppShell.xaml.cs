using Vyron.CustomerApp.Views.Auth;
using Vyron.CustomerApp.Views.Disputes;
using Vyron.CustomerApp.Views.More;
using Vyron.CustomerApp.Views.Orders;
using Vyron.CustomerApp.Views.Profile;
using Vyron.CustomerApp.Views.Reviews;
using Vyron.CustomerApp.Views.Stores;

namespace Vyron.CustomerApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        RegisterRoutes();

        // ── Stores-tab navigation reset ──────────────────────────────
        // When the user taps the Stores tab while sub-pages (StoreDetails,
        // ServiceSelection, etc.) are on the navigation stack, MAUI Shell does NOT
        // automatically pop them.  We listen to the Navigated event and, whenever
        // the shell lands back on the Stores root due to a tab-change or an
        // explicit //main/stores navigation, pop everything above the root page so
        // the user always sees a clean StoresPage (search/list).
        Navigated += OnShellNavigated;
    }

    private static async void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        var location = e.Current.Location.OriginalString;

        // React only when we have landed on the Stores tab root via tab interaction
        // (ShellSectionChanged = tab tapped; ShellItemChanged = tab-group switched)
        if (location == AppRoutes.Stores &&
            (e.Source == ShellNavigationSource.ShellSectionChanged ||
             e.Source == ShellNavigationSource.ShellItemChanged))
        {
            // Pop any stacked store/service-selection pages down to the root
            await Shell.Current.Navigation.PopToRootAsync(animated: false);
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
        Routing.RegisterRoute("pickupFeePayment", typeof(PickupFeePaymentPage));
        Routing.RegisterRoute("balancePayment",   typeof(BalancePaymentPage));

        // Dispute & review
        Routing.RegisterRoute("raiseDispute",     typeof(RaiseDisputePage));
        Routing.RegisterRoute("disputeHistory",   typeof(DisputeHistoryPage));
        Routing.RegisterRoute("addReview",        typeof(AddReviewPage));

        // Notifications (More tab)
        Routing.RegisterRoute("notifications",    typeof(NotificationsPage));

        // Message Rider (Track tab)
        Routing.RegisterRoute("messageRider",     typeof(MessageRiderPage));
    }
}
