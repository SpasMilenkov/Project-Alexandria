using System.Globalization;

namespace Alexandria.Services.Streaming.Wrapped;

/// <summary>Invariant-culture text formatting shared by the wrapped card builders.</summary>
internal static class ListeningStatsFormat
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Duration(long seconds)
    {
        if (seconds < 60) return $"{seconds} seconds";
        if (seconds < 3600) return $"{Number(seconds / 60.0)} minutes";

        return $"{Number(seconds / 3600.0)} hours";
    }

    public static string Plays(int count) => $"{count} {Plural(count, "play", "plays")}";

    public static string Plural(int count, string singular, string plural) => count == 1 ? singular : plural;

    public static string Percent(double value) => (value * 100).ToString("0", Invariant) + "%";

    public static string Number(double value) => value.ToString("0.#", Invariant);

    public static string Day(DateOnly date) => date.ToString("MMMM d", Invariant);

    public static string Day(DateTime date) => Day(DateOnly.FromDateTime(date));

    public static string MonthName(string isoDate) => DateOnly.Parse(isoDate, Invariant).ToString("MMMM", Invariant);

    public static string Iso(DateTime date) => date.ToString("o", Invariant);

    public static string IsoDate(DateOnly date) => date.ToString("yyyy-MM-dd", Invariant);
}
