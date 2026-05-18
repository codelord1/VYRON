using Vyron.CustomerApp.Views;
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
    private bool _resettingTab;

    public AppShell()
    {
        InitializeComponent();
        RegisterRoutes();

        // Keep bottom tabs predictable: selecting a tab lands on its root page.
        Navigated += OnShellNavigated;
    }

    public Task NavigateHomeRootAsync() => ResetTabToRootAsync(AppRoutes.Home);
    public Task NavigateStoresRootAsync() => ResetTabToRootAsync(AppRoutes.Stores);
    public Task NavigateOrdersRootAsync() => ResetTabToRootAsync(AppRoutes.Orders);
    public Task NavigateMoreRootAsync() => ResetTabToRootAsync(AppRoutes.More);

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
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

        Dispatcher.Dispatch(async () => await ResetTabToRootAsync(targetRoute));
    }

    private async Task ResetTabToRootAsync(string targetRoute)
    {
        if (_resettingTab)
            return;

        try
        {
            _resettingTab = true;
            if (CurrentState?.Location?.OriginalString == targetRoute)
                return;

            await GoToAsync(targetRoute, animate: false);
        }
        finally
        {
            _resettingTab = false;
        }
    }

    private static void RegisterRoutes()
    {
        Routing.RegisterRoute("verifyOtp", typeof(VerifyOtpPage));
        Routing.RegisterRoute("completeProfile", typeof(CompleteProfilePage));
        Routing.RegisterRoute("signup", typeof(SignupPage));
        Routing.RegisterRoute("forgotPassword", typeof(ForgotPasswordPage));
        Routing.RegisterRoute("resetPassword", typeof(ResetPasswordPage));

        Routing.RegisterRoute("profile", typeof(ProfilePage));

        Routing.RegisterRoute("storeDetails", typeof(StoreDetailsPage));

        Routing.RegisterRoute("serviceSelection", typeof(ServiceSelectionPage));
        Routing.RegisterRoute("createOrder", typeof(CreateOrderPage));
        Routing.RegisterRoute("orderSuccess", typeof(OrderSuccessPage));
        Routing.RegisterRoute("orderDetails", typeof(OrderDetailsPage));
        Routing.RegisterRoute("orderTracking", typeof(TrackPage));
        Routing.RegisterRoute("pickupFeePayment", typeof(PickupFeePaymentPage));
        Routing.RegisterRoute("balancePayment", typeof(BalancePaymentPage));

        Routing.RegisterRoute("raiseDispute", typeof(RaiseDisputePage));
        Routing.RegisterRoute("disputeHistory", typeof(DisputeHistoryPage));
        Routing.RegisterRoute("addReview", typeof(AddReviewPage));

        Routing.RegisterRoute("notifications", typeof(NotificationsPage));
        Routing.RegisterRoute("messageRider", typeof(MessageRiderPage));
    }
}
