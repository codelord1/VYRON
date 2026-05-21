using Vyron.CustomerApp.DTOs;

namespace Vyron.CustomerApp.Helpers;

public static class PhoneHelper
{
    public static readonly IReadOnlyList<CountryCodeOption> CountryOptions =
        new List<CountryCodeOption>
        {
            new() { FlagEmoji = "🇳🇬", CountryName = "Nigeria", DialCode = "+234", CountryIso = "NG" },
        };

    public static CountryCodeOption DefaultCountry => CountryOptions[0];

    public static string Normalize(string localPhone, string dialCode = "+234")
    {
        var digits = SanitizeLocalInput(localPhone, dialCode);
        return digits.Length == 10 ? $"0{digits}" : digits;
    }

    public static string SanitizeLocalInput(string localPhone, string dialCode = "+234")
    {
        var digits = new string((localPhone ?? string.Empty).Where(char.IsDigit).ToArray());
        var dialDigits = new string(dialCode.Where(char.IsDigit).ToArray());

        if (digits.StartsWith($"00{dialDigits}", StringComparison.Ordinal))
            digits = digits[(dialDigits.Length + 2)..];
        else if (digits.StartsWith(dialDigits, StringComparison.Ordinal))
            digits = digits[dialDigits.Length..];

        while (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length > 11)
            digits = digits[1..];

        return digits;
    }

    public static bool IsValid(string phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 11
            && digits.StartsWith("0", StringComparison.Ordinal)
            && digits[1..].All(char.IsDigit);
    }

    public static string NormalizeToE164(string localPhone, string dialCode = "+234")
    {
        var local = Normalize(localPhone, dialCode);
        var subscriber = local.StartsWith("0", StringComparison.Ordinal) ? local[1..] : local;
        return $"{dialCode}{subscriber}";
    }

    /// <summary>
    /// Converts any Nigerian phone input to E.164 format (+234XXXXXXXXXX).
    /// Handles all common variants:
    ///   07066364108      → +2347066364108  (local with leading 0)
    ///   7066364108       → +2347066364108  (bare 10-digit subscriber)
    ///   2347066364108    → +2347066364108  (numeric E.164)
    ///   +2347066364108   → +2347066364108  (already E.164 — passthrough)
    ///   23407066364108   → +2347066364108  (bad 2340 prefix — corrected)
    /// Returns "" for null/empty/whitespace input.
    /// </summary>
    public static string NormalizeForApi(string? localPhone, string dialCode = "+234")
    {
        if (string.IsNullOrWhiteSpace(localPhone)) return "";

        var digits     = new string(localPhone.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return "";

        var dialDigits = new string(dialCode.Where(char.IsDigit).ToArray()); // "234"

        // Fix bad "2340…" prefix (e.g. 23407066364108 → 2347066364108).
        // Happens when a leading-zero local number is naïvely prefixed with the dial code.
        if (digits.StartsWith($"{dialDigits}0", StringComparison.Ordinal) &&
            digits.Length == dialDigits.Length + 11)
            digits = dialDigits + digits[(dialDigits.Length + 1)..];

        // Already carries the country-code prefix (e.g. 2347066364108 or the stripped +2347066364108)
        if (digits.StartsWith(dialDigits, StringComparison.Ordinal))
            return $"+{digits}";

        // Local format: 07066364108  (leading 0 + 10 subscriber digits = 11 total)
        if (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length == 11)
            return $"+{dialDigits}{digits[1..]}";

        // Bare 10-digit subscriber: 7066364108
        if (digits.Length == 10)
            return $"+{dialDigits}{digits}";

        // Best-effort fallback — prepend dial code and let server validate
        return $"+{dialDigits}{digits}";
    }

    /// <summary>
    /// Returns true when <paramref name="phone"/> is a well-formed E.164 number
    /// for the given dial code — e.g. "+2347066364108" for Nigeria (+234).
    /// Expected pattern: "+" + dialDigits + exactly 10 subscriber digits.
    /// </summary>
    public static bool IsValidE164(string phone, string dialCode = "+234")
    {
        if (string.IsNullOrEmpty(phone)) return false;
        var dialDigits = new string(dialCode.Where(char.IsDigit).ToArray()); // "234"
        var prefix     = $"+{dialDigits}";                                   // "+234"
        return phone.Length == prefix.Length + 10
            && phone.StartsWith(prefix, StringComparison.Ordinal)
            && phone[prefix.Length..].All(char.IsDigit);
    }

    public static string FriendlyDuplicateMessage =>
        "This phone number already exists. Please login instead.";
}
