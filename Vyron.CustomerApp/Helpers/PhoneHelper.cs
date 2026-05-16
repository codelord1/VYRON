using Vyron.CustomerApp.DTOs;

namespace Vyron.CustomerApp.Helpers;

public static class PhoneHelper
{
    // ── Supported country codes (shown in the login picker) ──────────
    public static readonly IReadOnlyList<CountryCodeOption> CountryOptions =
        new List<CountryCodeOption>
        {
            new() { FlagEmoji = "🇳🇬", CountryName = "Nigeria",        DialCode = "+234", CountryIso = "NG" },
            new() { FlagEmoji = "🇬🇭", CountryName = "Ghana",          DialCode = "+233", CountryIso = "GH" },
            new() { FlagEmoji = "🇰🇪", CountryName = "Kenya",          DialCode = "+254", CountryIso = "KE" },
            new() { FlagEmoji = "🇿🇦", CountryName = "South Africa",   DialCode = "+27",  CountryIso = "ZA" },
            new() { FlagEmoji = "🇬🇧", CountryName = "United Kingdom", DialCode = "+44",  CountryIso = "GB" },
            new() { FlagEmoji = "🇺🇸", CountryName = "United States",  DialCode = "+1",   CountryIso = "US" },
            new() { FlagEmoji = "🇨🇦", CountryName = "Canada",         DialCode = "+1",   CountryIso = "CA" },
        };

    /// <summary>Default country (Nigeria).</summary>
    public static CountryCodeOption DefaultCountry => CountryOptions[0];

    /// <summary>
    /// Normalize a local phone number + dial code to E.164.
    /// Examples:
    ///   "08012345678" + "+234" → "+2348012345678"
    ///   "8012345678"  + "+234" → "+2348012345678"
    ///   "07911123456" + "+44"  → "+447911123456"
    ///   "+2348012345678"       → "+2348012345678" (already E.164 — returned as-is)
    /// </summary>
    public static string Normalize(string localPhone, string dialCode = "+234")
    {
        // Strip whitespace, dashes, parentheses
        var phone = localPhone.Trim()
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("(", "")
            .Replace(")", "");

        // Already E.164 → return as-is (handles if user typed full number)
        if (phone.StartsWith("+"))
            return phone;

        // Strip leading 0 (common in many countries: Nigeria 08xx, UK 07xx, etc.)
        if (phone.StartsWith("0"))
            phone = phone[1..];

        return $"{dialCode}{phone}";
    }

    /// <summary>Validates an E.164 phone number.</summary>
    public static bool IsValid(string phone)
    {
        var p = phone.Trim();
        return p.StartsWith("+")
            && p.Length >= 8   // +1 + 7 digits minimum
            && p.Length <= 16
            && p[1..].All(char.IsDigit);
    }
}
