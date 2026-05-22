namespace Vyron.DriverApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(RiderRoutes.OrderDetails, typeof(Views.OrderDetailsPage));
        Routing.RegisterRoute(RiderRoutes.ConfirmPickup, typeof(Views.ConfirmPickupPage));
        Routing.RegisterRoute(RiderRoutes.Delivered, typeof(Views.DeliveredPage));
        Routing.RegisterRoute(RiderRoutes.Notifications, typeof(Views.RiderNotificationsPage));
        Routing.RegisterRoute(RiderRoutes.Settings, typeof(Views.RiderSettingsPage));
    }
}

public static class RiderRoutes
{
    public const string Launch = "//launch";
    public const string Login = "//login";
    public const string Onboarding = "//onboarding";
    public const string Home = "//rider/homeTab/home";
    public const string Orders = "//rider/ordersTab/orders";
    public const string Map = "//rider/mapTab/map";
    public const string Earnings = "//rider/earningsTab/earnings";
    public const string Profile = "//rider/profileTab/profile";
    public const string OrderDetails = "orderDetails";
    public const string ConfirmPickup = "confirmPickup";
    public const string Delivered = "delivered";
    public const string Notifications = "notifications";
    public const string Settings = "settings";
}
