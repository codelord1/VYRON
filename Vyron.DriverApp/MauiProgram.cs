using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace Vyron.DriverApp;

public static class AppConstants
{
    public const string ApiBaseUrl = "http://10.0.2.2:5000/";
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
                f.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                f.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddHttpClient("VyronApi", c =>
        {
            c.BaseAddress = new Uri(AppConstants.ApiBaseUrl);
            c.Timeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddTransient<Views.MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
