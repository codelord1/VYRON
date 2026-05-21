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

    public static string FriendlyDuplicateMessage =>
        "This phone number already exists. Please login instead.";
}
