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
            SetBrandAliases(resources, dark: true);
            Set(resources, "Teal", Color.FromArgb("#008866"));
            Set(resources, "TealDark", Color.FromArgb("#006B50"));
            Set(resources, "Mint", Color.FromArgb("#123E35"));
            Set(resources, "MintSoft", Color.FromArgb("#0C231F"));
            Set(resources, "Dark", Color.FromArgb("#F4FBF8"));
            Set(resources, "Bg", Color.FromArgb("#07100F"));
            Set(resources, "CardStroke", Color.FromArgb("#214239"));
            Set(resources, "Gray3", Color.FromArgb("#A9BBB5"));
            Set(resources, "Gray5", Color.FromArgb("#F4FBF8"));
            Set(resources, "Danger", Color.FromArgb("#DC2626"));
            Set(resources, "Success", Color.FromArgb("#16A34A"));
            Set(resources, "Warning", Color.FromArgb("#F59E0B"));
            Set(resources, "InputBg", Color.FromArgb("#0E211D"));
            Set(resources, "OverlayBg", Color.FromArgb("#B3000000"));
            return;
        }

        SetBrandAliases(resources, dark: false);
        Set(resources, "Teal", Color.FromArgb("#008866"));
        Set(resources, "TealDark", Color.FromArgb("#006B50"));
        Set(resources, "Mint", Color.FromArgb("#E6F7F5"));
        Set(resources, "MintSoft", Color.FromArgb("#F2FBFA"));
        Set(resources, "Dark", Color.FromArgb("#006B50"));
        Set(resources, "Bg", Color.FromArgb("#F7F8FA"));
        Set(resources, "CardStroke", Color.FromArgb("#E5E7EB"));
        Set(resources, "Gray3", Color.FromArgb("#6B7280"));
        Set(resources, "Gray5", Color.FromArgb("#1F2937"));
        Set(resources, "Danger", Color.FromArgb("#DC2626"));
        Set(resources, "Success", Color.FromArgb("#16A34A"));
        Set(resources, "Warning", Color.FromArgb("#F59E0B"));
        Set(resources, "InputBg", Color.FromArgb("#FFFFFF"));
        Set(resources, "OverlayBg", Color.FromArgb("#99000000"));
    }

    private static void SetBrandAliases(ResourceDictionary resources, bool dark)
    {
        Set(resources, "VyronPrimary", Color.FromArgb("#006B50"));
        Set(resources, "VyronBlack", Color.FromArgb("#111111"));
        Set(resources, "VyronBackground", Color.FromArgb(dark ? "#07100F" : "#F7F8FA"));
        Set(resources, "VyronWhite", Color.FromArgb("#FFFFFF"));
        Set(resources, "VyronText", Color.FromArgb(dark ? "#F4FBF8" : "#1F2937"));
        Set(resources, "VyronAccent", Color.FromArgb("#008866"));
        Set(resources, "VyronSuccess", Color.FromArgb("#16A34A"));
        Set(resources, "VyronWarning", Color.FromArgb("#F59E0B"));
        Set(resources, "VyronError", Color.FromArgb("#DC2626"));
    }

    private static void Set(ResourceDictionary resources, string key, Color color)
    {
        if (resources.ContainsKey(key))
            resources[key] = color;
        else
            resources.Add(key, color);
    }
}
