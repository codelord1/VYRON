using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;

using Vyron.DriverApp.Services;

namespace Vyron.DriverApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(f =>
            {
                f.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                f.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if ANDROID
        EntryHandler.Mapper.AppendToMapping("RiderBorderlessEntry", (handler, _) =>
        {
            handler.PlatformView.Background = null;
        });
        EditorHandler.Mapper.AppendToMapping("RiderBorderlessEditor", (handler, _) =>
        {
            handler.PlatformView.Background = null;
        });
#endif

        builder.Services.AddSingleton<IRiderAuthService, MockRiderAuthService>();
        builder.Services.AddSingleton<IRiderJobService, MockRiderJobService>();
        builder.Services.AddSingleton<IRiderLocationService, MockRiderLocationService>();
        builder.Services.AddSingleton<IRiderNotificationService, MockRiderNotificationService>();
        builder.Services.AddSingleton<IRiderEarningsService, MockRiderEarningsService>();
        builder.Services.AddSingleton<IRiderProfileService, MockRiderProfileService>();

        builder.Services.AddTransient<Views.MainPage>();
        builder.Services.AddTransient<Views.LoginPage>();
        builder.Services.AddTransient<Views.RiderOnboardingPage>();
        builder.Services.AddTransient<Views.RiderHomePage>();
        builder.Services.AddTransient<Views.AssignedOrdersPage>();
        builder.Services.AddTransient<Views.RiderMapPage>();
        builder.Services.AddTransient<Views.OrderDetailsPage>();
        builder.Services.AddTransient<Views.ConfirmPickupPage>();
        builder.Services.AddTransient<Views.DeliveredPage>();
        builder.Services.AddTransient<Views.EarningsPage>();
        builder.Services.AddTransient<Views.RiderNotificationsPage>();
        builder.Services.AddTransient<Views.RiderProfilePage>();
        builder.Services.AddTransient<Views.RiderSettingsPage>();

        builder.Services.AddTransient<ViewModels.LoginViewModel>();
        builder.Services.AddTransient<ViewModels.RiderOnboardingViewModel>();
        builder.Services.AddTransient<ViewModels.RiderHomeViewModel>();
        builder.Services.AddTransient<ViewModels.AssignedOrdersViewModel>();
        builder.Services.AddTransient<ViewModels.RiderMapViewModel>();
        builder.Services.AddTransient<ViewModels.OrderDetailsViewModel>();
        builder.Services.AddTransient<ViewModels.ConfirmPickupViewModel>();
        builder.Services.AddTransient<ViewModels.DeliveredViewModel>();
        builder.Services.AddTransient<ViewModels.EarningsViewModel>();
        builder.Services.AddTransient<ViewModels.NotificationsViewModel>();
        builder.Services.AddTransient<ViewModels.ProfileViewModel>();
        builder.Services.AddTransient<ViewModels.SettingsViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
