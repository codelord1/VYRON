namespace Vyron.CustomerApp.Services;

public static class AppThemeService
{
    public const string PreferenceKey = "Vyron.Customer.Theme";
    public const string SystemTheme = "System";
    public const string LightTheme = "Light";
    public const string DarkTheme = "Dark";

    public static IReadOnlyList<string> ThemeOptions { get; } =
        new[] { SystemTheme, LightTheme, DarkTheme };

    public static string CurrentTheme
    {
        get => Preferences.Default.Get(PreferenceKey, SystemTheme);
        set
        {
            var normalized = Normalize(value);
            Preferences.Default.Set(PreferenceKey, normalized);
            Apply(normalized);
        }
    }

    public static void ApplySavedTheme() => Apply(CurrentTheme);

    public static void Apply(string? theme)
    {
        var normalized = Normalize(theme);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var app = Application.Current;
            if (app == null)
                return;

            app.UserAppTheme = normalized switch
            {
                LightTheme => AppTheme.Light,
                DarkTheme => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };

            var useDark = normalized == DarkTheme
                || (normalized == SystemTheme && app.RequestedTheme == AppTheme.Dark);

            ApplyPalette(app.Resources, useDark);
        });
    }

    public static void ApplySystemThemeIfNeeded()
    {
        if (CurrentTheme == SystemTheme)
            Apply(SystemTheme);
    }

    private static string Normalize(string? theme) =>
        theme switch
        {
            LightTheme => LightTheme,
            DarkTheme => DarkTheme,
            _ => SystemTheme
        };

    private static void ApplyPalette(ResourceDictionary resources, bool dark)
    {
        if (dark)
        {
            Set(resources, "Teal", Color.FromArgb("#11B98E"));
            Set(resources, "TealDark", Color.FromArgb("#0B8F6A"));
            Set(resources, "Mint", Color.FromArgb("#123E35"));
            Set(resources, "MintSoft", Color.FromArgb("#0C231F"));
            Set(resources, "Dark", Color.FromArgb("#F4FBF8"));
            Set(resources, "Bg", Color.FromArgb("#061713"));
            Set(resources, "CardStroke", Color.FromArgb("#214239"));
            Set(resources, "Gray3", Color.FromArgb("#A9BBB5"));
            Set(resources, "Gray5", Color.FromArgb("#F4FBF8"));
            Set(resources, "Danger", Color.FromArgb("#FF6673"));
            Set(resources, "Success", Color.FromArgb("#34D399"));
            Set(resources, "Warning", Color.FromArgb("#FBBF24"));
            Set(resources, "InputBg", Color.FromArgb("#0E211D"));
            Set(resources, "OverlayBg", Color.FromArgb("#B3000000"));
            return;
        }

        Set(resources, "Teal", Color.FromArgb("#008866"));
        Set(resources, "TealDark", Color.FromArgb("#006B50"));
        Set(resources, "Mint", Color.FromArgb("#D9F8EC"));
        Set(resources, "MintSoft", Color.FromArgb("#EEF8F4"));
        Set(resources, "Dark", Color.FromArgb("#061F1A"));
        Set(resources, "Bg", Color.FromArgb("#F4FAF7"));
        Set(resources, "CardStroke", Color.FromArgb("#DCE9E4"));
        Set(resources, "Gray3", Color.FromArgb("#697780"));
        Set(resources, "Gray5", Color.FromArgb("#071A1F"));
        Set(resources, "Danger", Color.FromArgb("#F04452"));
        Set(resources, "Success", Color.FromArgb("#0B8F6A"));
        Set(resources, "Warning", Color.FromArgb("#D68A00"));
        Set(resources, "InputBg", Color.FromArgb("#FFFFFF"));
        Set(resources, "OverlayBg", Color.FromArgb("#99000000"));
    }

    private static void Set(ResourceDictionary resources, string key, Color color)
    {
        if (resources.ContainsKey(key))
            resources[key] = color;
        else
            resources.Add(key, color);
    }
}
