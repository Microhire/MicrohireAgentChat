using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services.Persistence;

/// <summary>
/// Shared normalization for contact deduplication (email, phone) and organisation name keys.
/// </summary>
public static class ContactLookupNormalization
{
    /// <summary>Lowercase trimmed email, or null if empty.</summary>
    public static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        return email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Normalizes a phone string to comparable digits (AU-oriented: 04xxxxxxxx, handles +61).
    /// Returns null if too few digits to match safely.
    /// </summary>
    public static string? NormalizePhoneDigits(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 8) return null;

        // AU international: 61 4xx xxx xxx -> 04xxxxxxxx
        if (digits.StartsWith("61", StringComparison.Ordinal) && digits.Length >= 11)
            digits = "0" + digits[2..];

        // 9 digits starting with 4 → mobile missing leading 0
        if (digits.Length == 9 && digits[0] == '4')
            digits = "0" + digits;

        if (digits.Length > 10 && digits.StartsWith('0'))
            digits = digits[..10];

        return digits.Length >= 8 ? digits : null;
    }

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Collapses whitespace, trims punctuation; lowercases for case-insensitive comparison.
    /// Strips common legal suffixes for lookup only (not display).
    /// </summary>
    public static string NormalizeOrganisationKey(string? organisation)
    {
        if (string.IsNullOrWhiteSpace(organisation)) return string.Empty;
        var t = Whitespace.Replace(organisation.Trim(), " ");
        t = t.Trim(';', ':', '.', ',');
        var lower = t.ToLowerInvariant();

        string[] suffixes =
        [
            " pty. ltd.",
            " pty ltd.",
            " pty ltd",
            " limited",
            " ltd.",
            " ltd",
            " inc.",
            " inc",
            " abn",
        ];

        foreach (var s in suffixes)
        {
            if (lower.EndsWith(s, StringComparison.Ordinal))
            {
                lower = lower[..^s.Length].TrimEnd();
                break;
            }
        }

        return lower;
    }
}
