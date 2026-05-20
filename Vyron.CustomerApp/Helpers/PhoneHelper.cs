using Vyron.CustomerApp.DTOs;

namespace Vyron.CustomerApp.Helpers;

public static class PhoneHelper
{
    public static readonly IReadOnlyList<CountryCodeOption> CountryOptions =
        new List<CountryCodeOption>
        {
            new() { FlagEmoji = "NG", CountryName = "Nigeria", DialCode = "+234", CountryIso = "NG" },
        };

    public static CountryCodeOption DefaultCountry => CountryOptions[0];

    public static string Normalize(string localPhone, string dialCode = "+234")
    {
        var digits = SanitizeLocalInput(localPhone, dialCode);
        return $"{dialCode}{digits}";
    }

    public static string SanitizeLocalInput(string localPhone, string dialCode = "+234")
    {
        var digits = new string(localPhone.Where(char.IsDigit).ToArray());
        var dialDigits = new string(dialCode.Where(char.IsDigit).ToArray());

        if (digits.StartsWith(dialDigits, StringComparison.Ordinal))
            digits = digits[dialDigits.Length..];

        while (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length > 1)
            digits = digits[1..];

        return digits;
    }

    public static bool IsValid(string phone)
    {
        var p = phone.Trim();
        return p.StartsWith("+", StringComparison.Ordinal)
            && p.Length >= 8
            && p.Length <= 16
            && p[1..].All(char.IsDigit);
    }

    public static string FriendlyDuplicateMessage =>
        "This phone number already exists. Please login instead.";
}
