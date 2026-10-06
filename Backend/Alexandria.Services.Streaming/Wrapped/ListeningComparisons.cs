using Alexandria.Data.Models.Enumerators;
using System.Security.Cryptography;
using System.Text;
using Alexandria.Dto.Files.Streaming.Stats;

namespace Alexandria.Services.Streaming.Wrapped;

internal static class ListeningComparisons
{
    internal const string CatalogVersion = "2026-10-06.1";

    private const double MinComparableMinutes = 120;
    private const double CloseDistance = 0.1;
    private const double NearestTolerance = 0.02;
    private const int MaxAlternatives = 2;

    private static readonly Lazy<IReadOnlyList<DurationAnchor>> Anchors = new(DurationCatalogLoader.Load);

    internal static int CatalogCount => Anchors.Value.Count;

    internal static WrappedDurationTier TierFor(double minutes)
    {
        if (minutes < 120) return WrappedDurationTier.UnderTwoHours;
        if (minutes < 180) return WrappedDurationTier.TwoToThree;
        if (minutes < 360) return WrappedDurationTier.ThreeToSix;
        if (minutes < 720) return WrappedDurationTier.SixToTwelve;
        if (minutes < 1200) return WrappedDurationTier.TwelveToTwenty;
        if (minutes < 2400) return WrappedDurationTier.TwentyToForty;
        if (minutes < 4800) return WrappedDurationTier.FortyToEighty;
        if (minutes < 7200) return WrappedDurationTier.EightyToOneTwenty;

        return WrappedDurationTier.OneTwentyPlus;
    }

    public static IReadOnlyList<WrappedComparison> Select(long seconds, string identity)
    {
        var minutes = seconds / 60.0;

        if (minutes < MinComparableMinutes) return [];

        var close = CloseAnchors(minutes, identity);

        if (close.Count > 0) return FrameClose(close, minutes, identity);

        var passed = LastPassedAnchor(minutes, identity);

        if (passed == null) return [];

        return [Frame(passed, minutes, passedMilestone: true)];
    }

    // Anchors in the same tier that are within range or within 10% of the listening time, nearest first.
    private static List<DurationAnchor> CloseAnchors(double minutes, string identity)
    {
        var tier = TierFor(minutes);

        return Anchors.Value.Where(a => TierFor(a.DurationMinutes) == tier && IsClose(a, minutes))
            .OrderBy(a => Distance(a, minutes)).ThenBy(a => TieKey(identity, a.Id), StringComparer.Ordinal)
            .ToList();
    }

    // The nearest anchor is the primary pick; up to two more from other categories are offered as alternatives.
    private static List<WrappedComparison> FrameClose(List<DurationAnchor> close, double minutes, string identity)
    {
        var nearest = Distance(close[0], minutes);

        var primary = close.Where(a => Distance(a, minutes) <= nearest + NearestTolerance)
            .OrderBy(a => TieKey(identity, a.Id), StringComparer.Ordinal).First();

        var alternatives = close.Where(a => a.Id != primary.Id && a.Category != primary.Category)
            .GroupBy(a => a.Category).Select(g => g.First()).Take(MaxAlternatives);

        return new[] { primary }.Concat(alternatives).Select(a => Frame(a, minutes)).ToList();
    }

    private static DurationAnchor? LastPassedAnchor(double minutes, string identity)
    {
        return Anchors.Value.Where(a => Upper(a) <= minutes)
            .OrderByDescending(Upper).ThenBy(a => TieKey(identity, a.Id), StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static WrappedComparison Frame(DurationAnchor a, double minutes, bool passedMilestone = false)
    {
        var name = DisplayName(a);

        var (relationship, copy) = Describe(a, minutes, name, passedMilestone);

        return new WrappedComparison(a.Id, name, DurationCatalogLoader.CategoryFor(a.Category),
            TierFor(a.DurationMinutes), a.DurationMinutes, a.DurationRangeMinutes, a.DurationLabel, a.Exact,
            relationship, copy, CatalogVersion, a.Copy.Trim());
    }

    private static string DisplayName(DurationAnchor a)
    {
        return a.Id == "marathon_world_record_kelvin_kiptum" ? "Kelvin Kiptum's 2:00:35 marathon" : a.Name;
    }

    private static (WrappedDurationRelationship Relationship, string Copy) Describe(DurationAnchor a, double minutes,
        string name, bool passedMilestone)
    {
        if (InRange(a, minutes))
            return (WrappedDurationRelationship.WithinRange, $"Within the time it takes for {name}.");

        if (passedMilestone)
            return (WrappedDurationRelationship.MilestonePassed, $"You passed a milestone: {name}.");

        return (WrappedDurationRelationship.Approximately, $"About as much time as {name}.");
    }

    private static double Upper(DurationAnchor a) => a.DurationRangeMinutes?[^1] ?? a.DurationMinutes;

    private static bool InRange(DurationAnchor a, double minutes) =>
        a.DurationRangeMinutes is { Length: 2 } range && minutes >= range[0] && minutes <= range[1];

    private static bool IsClose(DurationAnchor a, double minutes) =>
        InRange(a, minutes) || Distance(a, minutes) <= CloseDistance;

    private static double Distance(DurationAnchor a, double minutes) =>
        Math.Abs(minutes - a.DurationMinutes) / a.DurationMinutes;

    // A stable per-user shuffle: the hash decides between anchors that are equally good matches.
    private static string TieKey(string identity, string id) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{CatalogVersion}:{identity}:{id}")));
}
