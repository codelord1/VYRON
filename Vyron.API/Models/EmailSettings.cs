namespace Vyron.API.Models;

/// <summary>
/// Strongly-typed SMTP email configuration.
/// Non-secret values live in appsettings.json / appsettings.Development.json.
/// Email:Password must NEVER appear in any settings file — read from
/// dotnet user-secrets (Development) or environment variable (Production).
/// </summary>
public class EmailSettings
{
    /// <summary>Master switch. If false, all sends are skipped gracefully.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Human-readable provider label used in logs (e.g. "Gmail").</summary>
    public string Provider { get; set; } = "Gmail";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;

    /// <summary>Use SSL/TLS on connect (port 465). Mutually exclusive with UseStartTls.</summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>Upgrade to TLS after connect (port 587). Preferred for Gmail.</summary>
    public bool UseStartTls { get; set; } = true;

    public string Username { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "VYRON";

    /// <summary>
    /// SMTP password / Gmail App Password.
    /// Set via: dotnet user-secrets set "Email:Password" "&lt;APP_PASSWORD&gt;"
    /// In Production: set environment variable Email__Password.
    /// NEVER hardcode here or in appsettings files.
    /// </summary>
    public string Password { get; set; } = "";

    public int TimeoutSeconds { get; set; } = 15;

    // ── Computed helpers ──────────────────────────────────────────────
    public bool IsFullyConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password);
}
