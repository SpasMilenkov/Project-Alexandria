using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.LyricsStats;

public enum LyricsStatsBucket
{
    Hour,
    Day
}

public sealed record LyricsStatusCount(LyricsStatus Status, int Count);

// Terminal outcomes only: Fetched + NoMatch + FetchFailed. FetchFailed is the
// numerator; PendingFetch/Fetching are in-flight and excluded.
public sealed record LyricsRatePoint(
    DateTime BucketStart,
    int Total,
    int Fetched,
    int Failed)
{
    public double FailureRate => Total == 0 ? 0 : Math.Round(100.0 * Failed / Total, 2);
}

public sealed record LyricsProviderBreakdownRow(
    LyricsProvider Provider,
    int Total,
    int Failed);

public sealed record LyricsTrendResponse(
    DateTime From,
    DateTime To,
    LyricsStatsBucket Bucket,
    IReadOnlyList<LyricsRatePoint> Points);

public sealed record LyricsOverviewResponse(
    IReadOnlyList<LyricsStatusCount> StatusCounts,
    IReadOnlyList<LyricsProviderBreakdownRow> Providers,
    long FetchedTotal,
    double? AvgConfidence);