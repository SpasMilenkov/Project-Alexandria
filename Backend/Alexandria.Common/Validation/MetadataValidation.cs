using System.Text.RegularExpressions;

namespace Alexandria.Common.Validation;

/// <summary>
/// Shared input rules for user-editable media metadata. Used by the single and bulk
/// file-metadata validators so both endpoints reject the same values.
/// </summary>
public static partial class MetadataValidation
{
    [GeneratedRegex(@"^\d{4}$")]
    private static partial Regex FourDigitYearRegex();

    /// <summary>
    /// A year is valid when it is exactly four digits and falls between 1000 and next
    /// year (inclusive), leaving room for announced releases without accepting typos
    /// like 2221. Null/empty is not valid here; optionality is expressed by the caller
    /// leaving the field out entirely.
    /// </summary>
    public static bool IsValidYear(string? year)
    {
        if (year is null) return false;
        if (!FourDigitYearRegex().IsMatch(year)) return false;
        if (!int.TryParse(year, out var value)) return false;
        return value >= 1000 && value <= DateTime.UtcNow.Year + 1;
    }
}