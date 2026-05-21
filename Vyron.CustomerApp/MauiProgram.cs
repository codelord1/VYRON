using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Vyron.CustomerApp.Services;
using Vyron.CustomerApp.ViewModels;
using Vyron.CustomerApp.Views;
using Vyron.CustomerApp.Views.Auth;
using Vyron.CustomerApp.Views.Disputes;
using Vyron.CustomerApp.Views.Orders;
using Vyron.CustomerApp.Views.Profile;
using Vyron.CustomerApp.Views.Reviews;
using Vyron.CustomerApp.Views.More;
using Vyron.CustomerApp.Views.Stores;

namespace Vyron.CustomerApp;

public static class AppConstants
{
    // ── LOCAL DEVELOPMENT (Android Emulator) ────────────────────────
    // Android emulator maps 10.0.2.2 → host machine's 127.0.0.1 (loopback).
    //
    // ⚠️  IMPORTANT — Run Vyron.API with the KESTREL profile, NOT IIS Express.
    //     In Visual Studio: change the run dropdown from "IIS Express" to "Vyron.API".
    //     IIS Express rejects requests whose Host header is not "localhost", so
    //     any call from the Android emulator (Host: 10.0.2.2) gets a blank 400.
    //
    // Kestrel ports (Vyron.API/Properties/launchSettings.json):
    //   HTTP  → http://localhost:50680   ← use this (no cert issues)
    //   HTTPS → https://localhost:50677  ← requires the DEBUG cert bypass below
    //
    // HTTP is used here to avoid dev-certificate problems on the emulator.
    // The DEBUG cert bypass block below is kept in case you switch to HTTPS.
    //
    // ── PHYSICAL DEVICE ─────────────────────────────────────────────
    // Replace with your LAN IP, e.g. http://192.168.1.10:50680/
    //
    // ── PRODUCTION ──────────────────────────────────────────────────
    // Replace with https://api.vyron.com/
    // The DEBUG certificate bypass is NOT compiled into Release builds.
    //public const string ApiBaseUrl = "http://10.0.2.2:50680/";
    //public const string SignalRUrl  = "http://10.0.2.2:50680/hubs/tracking";

    public const string ApiBaseUrl = "http://192.168.0.166:50680/";
    public const string SignalRUrl = "http://192.168.0.166:50680/hubs/tracking";

    // ── VYRON SUPPORT CONTACT ────────────────────────────────────────
    // TODO: Replace these with values fetched from Admin Portal → System Settings API
    //       once the backend exposes a /api/settings/public or similar endpoint.
    //       At that point, inject a SettingsService and consume it in StoreDetailsViewModel.
    public const string SupportPhoneNumber = "";  // e.g. "+234 800 000 0000" — set via Admin Portal
    public const string SupportEmail       = "";  // e.g. "support@vyron.com"  — set via Admin Portal
    public const string SupportDisplayText = "For issues, complaints, and order concerns";
}

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
                f.AddFont("OpenSans-Regular.ttf",   "OpenSansRegular");
                f.AddFont("OpenSans-Semibold.ttf",  "OpenSansSemibold");
            });

        // ── HTTP Client ─────────────────────────────────────────────
#if DEBUG
        // DEBUG ONLY: bypass SSL certificate validation for 10.0.2.2 (Android emulator
        // pointing at the host machine's self-signed Visual Studio dev certificate).
        // This handler is EXCLUDED from Release builds by the preprocessor — it never
        // reaches production. All other hosts still require a valid certificate.
        builder.Services.AddHttpClient("VyronApi", c =>
        {
            c.BaseAddress = new Uri(AppConstants.ApiBaseUrl);
            c.Timeout     = TimeSpan.FromSeconds(30);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                // Only bypass for the local dev host — any real hostname still validates.
                if (message.RequestUri?.Host is "10.0.2.2" or "localhost")
                    return true;
                return errors == System.Net.Security.SslPolicyErrors.None;
            }
        });
#else
        // RELEASE: standard HttpClient — full certificate validation, no bypass.
        builder.Services.AddHttpClient("VyronApi", c =>
        {
            c.BaseAddress = new Uri(AppConstants.ApiBaseUrl);
            c.Timeout     = TimeSpan.FromSeconds(30);
        });
#endif

        // ── Core Services ───────────────────────────────────────────
        builder.Services.AddSingleton<ApiService>();
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<StoreService>();
        builder.Services.AddSingleton<OrderService>();
        builder.Services.AddSingleton<PaymentService>();
        builder.Services.AddSingleton<DisputeService>();
        builder.Services.AddSingleton<ReviewService>();
        builder.Services.AddSingleton<ProfileService>();
        builder.Services.AddSingleton<OrderDraftService>();  // shared cart across service-selection → checkout
        builder.Services.AddSingleton<NotificationService>();
        builder.Services.AddSingleton<RiderMessageService>();

        // ── Shell ────────────────────────────────────────────────────
        builder.Services.AddSingleton<AppShell>();

        // ── ViewModels ───────────────────────────────────────────────
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<SignupViewModel>();
        builder.Services.AddTransient<VerifyOtpViewModel>();
        builder.Services.AddTransient<CompleteProfileViewModel>();
        builder.Services.AddTransient<ForgotPasswordViewModel>();
        builder.Services.AddTransient<ResetPasswordViewModel>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<StoresViewModel>();
        builder.Services.AddTransient<StoreDetailsViewModel>();
        builder.Services.AddTransient<ServiceSelectionViewModel>();
        builder.Services.AddTransient<CreateOrderViewModel>();
        builder.Services.AddTransient<OrderSuccessViewModel>();
        builder.Services.AddTransient<PickupFeeViewModel>();
        builder.Services.AddTransient<OrdersViewModel>();
        builder.Services.AddTransient<OrderDetailsViewModel>();
        builder.Services.AddTransient<BalancePaymentViewModel>();
        builder.Services.AddTransient<RaiseDisputeViewModel>();
        builder.Services.AddTransient<DisputeHistoryViewModel>();
        builder.Services.AddTransient<AddReviewViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<TrackViewModel>();
        builder.Services.AddTransient<MoreViewModel>();
        builder.Services.AddTransient<NotificationsViewModel>();
        builder.Services.AddTransient<MessageRiderViewModel>();

        // ── Pages ────────────────────────────────────────────────────
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<SignupPage>();
        builder.Services.AddTransient<VerifyOtpPage>();
        builder.Services.AddTransient<CompleteProfilePage>();
        builder.Services.AddTransient<ForgotPasswordPage>();
        builder.Services.AddTransient<ResetPasswordPage>();
        builder.Services.AddTransient<PickupLocationPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<StoresPage>();
        builder.Services.AddTransient<StoreDetailsPage>();
        builder.Services.AddTransient<ServiceSelectionPage>();
        builder.Services.AddTransient<CreateOrderPage>();
        builder.Services.AddTransient<OrderSuccessPage>();
        builder.Services.AddTransient<PickupFeePaymentPage>();
        builder.Services.AddTransient<OrdersPage>();
        builder.Services.AddTransient<OrderDetailsPage>();
        builder.Services.AddTransient<BalancePaymentPage>();
        builder.Services.AddTransient<RaiseDisputePage>();
        builder.Services.AddTransient<DisputeHistoryPage>();
        builder.Services.AddTransient<AddReviewPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<TrackPage>();
        builder.Services.AddTransient<MorePage>();
        builder.Services.AddTransient<NotificationsPage>();
        builder.Services.AddTransient<MessageRiderPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
