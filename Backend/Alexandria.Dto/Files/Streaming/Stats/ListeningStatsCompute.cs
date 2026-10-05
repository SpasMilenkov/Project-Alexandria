using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files.Streaming.Stats;

/// <summary>
/// One stream session inside the stats period, with its joined metadata projected.
/// The repository fetch builds these once; compute buckets in memory over them.
/// </summary>
public sealed record ListeningSessionRow(
    Guid SessionId,
    Guid FileId,
    string FileName,
    string? Title,
    string? Artist,
    DateTime StartedAt,
    long ListenedSeconds,
    bool ReachedCompletionThreshold);

/// <summary>
/// Whole-history lightweight ref feeding the discovery metrics. One row per
/// (user, file) history, not per session; the period join alone cannot see
/// first-seen dates outside the range.
/// </summary>
public sealed record ListeningHistoryRef(
    Guid FileId,
    string? Artist,
    DateTime HistoryCreatedAt,
    DateTime? LastBeforePeriod = null);

/// <summary>One ranked artist entry, ordered by total listened seconds descending.</summary>
public sealed record RankedArtist(
    string Key,
    string DisplayName,
    long TotalListenedSeconds,
    int SessionCount);

/// <summary>One ranked song entry, ordered by total listened seconds descending.</summary>
public sealed record RankedSong(
    Guid FileId,
    string DisplayTitle,
    string? DisplayArtist,
    long TotalListenedSeconds,
    int SessionCount);

/// <summary>Replay extreme: most sessions on one file.</summary>
public sealed record ReplayExtreme(Guid FileId, int SessionCount, int ActiveDays = 0);

/// <summary>Duration extreme: most seconds on one file.</summary>
public sealed record DurationExtreme(Guid FileId, long TotalListenedSeconds);

/// <summary>Longest single sitting.</summary>
public sealed record LongestSitting(Guid SessionId, Guid FileId, long ListenedSeconds, DateTime StartedAt);

/// <summary>Period edge session.</summary>
public sealed record BookendSession(Guid FileId, DateTime StartedAt);

/// <summary>Newly discovered artist whose first-seen date falls in the period.</summary>
public sealed record DiscoveredArtist(string Key, string DisplayName);

/// <summary>
/// Compute-layer output: every raw number behind the metric catalog, no narrative
/// decisions. UTC throughout; empty periods yield <see cref="HasSessions"/>
/// false with empty collections, never null.
/// </summary>
public sealed record RawListeningStats(
    bool HasSessions,
    long TotalListenedSeconds,
    int SessionCount,
    IReadOnlyList<RankedArtist> TopArtists,
    IReadOnlyList<RankedSong> TopSongs,
    ReplayExtreme? MostReplayed,
    DurationExtreme? MostMinutesOnSong,
    IReadOnlyList<long> SecondsByHourUtc,
    TimeOfDayBucket DominantBucket,
    long WeekdaySeconds,
    long WeekendSeconds,
    int LongestStreakDays,
    DateOnly? StreakStart,
    DateOnly? StreakEnd,
    DateOnly? BusiestDay,
    long BusiestDaySeconds,
    LongestSitting? LongestSitting,
    BookendSession? FirstSession,
    BookendSession? LastSession,
    int DiscoveryCount,
    IReadOnlyList<DiscoveredArtist> NewArtists,
    double CompletionRate,
    int CompletedSessions)
{
    public ListeningInsights Insights { get; init; } = new();
}

public sealed record ListeningPeriodPoint(
    string Date,
    long Seconds,
    int Plays,
    string? Leader = null,
    string? LeaderKey = null);

public sealed record ListeningDiscovery(
    Guid FileId,
    string Title,
    string? Artist,
    DateTime FirstHeard,
    int ActiveDays,
    long Seconds);

public sealed record ListeningReturn(
    Guid FileId,
    string Title,
    string? Artist,
    DateTime PreviousPlay,
    DateTime ReturnedAt,
    int GapDays);

/// <summary>Bounded, factual signals shared by curation and artwork. All shares use total listening seconds.</summary>
public sealed record ListeningInsights
{
    public int ActiveDays { get; init; }
    public double MedianDaySeconds { get; init; }
    public double MedianSessionSeconds { get; init; }
    public double KnownArtistShare { get; init; }
    public double TopArtistShare { get; init; }
    public double TopSongShare { get; init; }
    public double DominantShare { get; init; }
    public double DominantMargin { get; init; }
    public double NewListeningShare { get; init; }
    public bool HasPriorHistory { get; init; }
    public DateTime? HistoryFrom { get; init; }
    public IReadOnlyList<ListeningPeriodPoint> Days { get; init; } = [];
    public IReadOnlyList<ListeningPeriodPoint> Months { get; init; } = [];
    public IReadOnlyList<ListeningPeriodPoint> Chapters { get; init; } = [];
    public ListeningDiscovery? RetainedDiscovery { get; init; }
    public ListeningReturn? ReturningFavorite { get; init; }
}