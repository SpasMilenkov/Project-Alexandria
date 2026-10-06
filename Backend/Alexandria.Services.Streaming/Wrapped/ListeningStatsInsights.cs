using System.Globalization;
using Alexandria.Dto.Files.Streaming.Stats;
using static Alexandria.Services.Streaming.Wrapped.ListeningStatsFormat;

namespace Alexandria.Services.Streaming.Wrapped;

internal static class ListeningStatsInsights
{
    private const int MinRetainedDiscoveryDays = 3;
    private const int MinRetainedDiscoverySeconds = 600;
    private const int MinReturnGapDays = 30;
    private const int ChapterMinPlays = 10;
    private const int ChapterMinSeconds = 1800;
    private const int ChapterMinDays = 3;

    public static ListeningInsights Build(DateTime from, DateTime to, List<ListeningSessionRow> rows,
        List<ListeningHistoryRef> histories, IReadOnlyList<RankedArtist> artists,
        IReadOnlyList<RankedSong> songs, long[] buckets)
    {
        var total = (double)rows.Sum(r => r.ListenedSeconds);
        var byDay = rows.GroupBy(r => DateOnly.FromDateTime(r.StartedAt)).ToDictionary(g => g.Key, g => g.ToList());

        var byMonth = rows.GroupBy(r => new DateOnly(r.StartedAt.Year, r.StartedAt.Month, 1))
            .ToDictionary(g => g.Key, g => g.ToList());

        var months = MonthlyPoints(from, to, byMonth, artists);

        var firstByFile = histories.GroupBy(h => h.FileId)
            .ToDictionary(g => g.Key, g => g.MinBy(h => h.HistoryCreatedAt)!);

        var orderedBuckets = buckets.OrderDescending().ToArray();

        return new ListeningInsights
        {
            ActiveDays = byDay.Count,
            MedianDaySeconds = Median(byDay.Values.Select(v => v.Sum(r => r.ListenedSeconds))),
            MedianSessionSeconds = Median(rows.Select(r => r.ListenedSeconds)),
            KnownArtistShare = artists.Sum(a => a.TotalListenedSeconds) / total,
            TopArtistShare = (artists.FirstOrDefault()?.TotalListenedSeconds ?? 0) / total,
            TopSongShare = (songs.FirstOrDefault()?.TotalListenedSeconds ?? 0) / total,
            DominantShare = orderedBuckets[0] / total,
            DominantMargin = (orderedBuckets[0] - orderedBuckets[1]) / total,
            NewListeningShare = NewListeningSeconds(rows, firstByFile, from) / total,
            HasPriorHistory = histories.Any(h => h.HistoryCreatedAt < from),
            HistoryFrom = histories.Select(h => (DateTime?)h.HistoryCreatedAt).Min(),
            Days = DailyPoints(from, to, byDay),
            Months = months,
            Chapters = ChapterMonths(months, byMonth),
            RetainedDiscovery = FindRetainedDiscovery(rows, firstByFile, songs, from),
            ReturningFavorite = FindReturningFavorite(rows, firstByFile, songs)
        };
    }

    private static List<ListeningPeriodPoint> DailyPoints(DateTime from, DateTime to,
        Dictionary<DateOnly, List<ListeningSessionRow>> byDay)
    {
        var days = new List<ListeningPeriodPoint>();

        for (var day = DateOnly.FromDateTime(from);
             day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) < to;
             day = day.AddDays(1))
        {
            var samples = byDay.GetValueOrDefault(day) ?? [];

            days.Add(new ListeningPeriodPoint(IsoDate(day), samples.Sum(r => r.ListenedSeconds),
                samples.Count(r => r.IsQualifiedPlay())));
        }

        return days;
    }

    private static List<ListeningPeriodPoint> MonthlyPoints(DateTime from, DateTime to,
        Dictionary<DateOnly, List<ListeningSessionRow>> byMonth, IReadOnlyList<RankedArtist> artists)
    {
        var months = new List<ListeningPeriodPoint>();

        for (var month = new DateOnly(from.Year, from.Month, 1);
             month.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) < to;
             month = month.AddMonths(1))
        {
            var samples = byMonth.GetValueOrDefault(month) ?? [];
            var leader = LeadingArtist(samples);
            var leaderName = artists.FirstOrDefault(a => a.Key == leader?.Key)?.DisplayName;

            months.Add(new ListeningPeriodPoint(IsoDate(month), samples.Sum(r => r.ListenedSeconds),
                samples.Count(r => r.IsQualifiedPlay()),
                leaderName, leader?.Key));
        }

        return months;
    }

    private static IGrouping<string, ListeningSessionRow>? LeadingArtist(List<ListeningSessionRow> samples)
    {
        return samples.Where(r => ListeningStatsCompute.NormalizeArtist(r.Artist) != null)
            .GroupBy(r => ListeningStatsCompute.NormalizeArtist(r.Artist)!)
            .OrderByDescending(g => g.Sum(r => r.ListenedSeconds)).ThenBy(g => g.Key, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static List<ListeningPeriodPoint> ChapterMonths(List<ListeningPeriodPoint> months,
        Dictionary<DateOnly, List<ListeningSessionRow>> byMonth)
    {
        return months.Where(m => m.Plays >= ChapterMinPlays && m.Seconds >= ChapterMinSeconds && m.LeaderKey != null
                                 && byMonth[DateOnly.Parse(m.Date, CultureInfo.InvariantCulture)]
                                     .Select(r => r.StartedAt.Date).Distinct().Count() >= ChapterMinDays)
            .ToList();
    }

    private static bool FirstHeardSince(Dictionary<Guid, ListeningHistoryRef> firstByFile, Guid fileId, DateTime from)
    {
        return firstByFile.TryGetValue(fileId, out var history) && history.HistoryCreatedAt >= from;
    }

    private static long NewListeningSeconds(List<ListeningSessionRow> rows,
        Dictionary<Guid, ListeningHistoryRef> firstByFile, DateTime from)
    {
        return rows.Where(r => FirstHeardSince(firstByFile, r.FileId, from)).Sum(r => r.ListenedSeconds);
    }

    private static ListeningDiscovery? FindRetainedDiscovery(List<ListeningSessionRow> rows,
        Dictionary<Guid, ListeningHistoryRef> firstByFile, IReadOnlyList<RankedSong> songs, DateTime from)
    {
        return rows.GroupBy(r => r.FileId)
            .Where(g => FirstHeardSince(firstByFile, g.Key, from))
            .Select(g =>
            {
                var song = songs.First(s => s.FileId == g.Key);

                return new ListeningDiscovery(g.Key, song.DisplayTitle, song.DisplayArtist,
                    firstByFile[g.Key].HistoryCreatedAt,
                    g.Select(r => DateOnly.FromDateTime(r.StartedAt)).Distinct().Count(),
                    g.Sum(r => r.ListenedSeconds));
            })
            .Where(d => d.ActiveDays >= MinRetainedDiscoveryDays && d.Seconds >= MinRetainedDiscoverySeconds)
            .OrderByDescending(d => d.Seconds).ThenBy(d => d.FileId)
            .FirstOrDefault();
    }

    private static ListeningReturn? FindReturningFavorite(List<ListeningSessionRow> rows,
        Dictionary<Guid, ListeningHistoryRef> firstByFile, IReadOnlyList<RankedSong> songs)
    {
        return rows.GroupBy(r => r.FileId)
            .SelectMany(group => ReturnsFor(group, firstByFile, songs))
            .OrderByDescending(r => r.GapDays).ThenBy(r => r.FileId)
            .FirstOrDefault();
    }

    // A return needs listening on both sides of a real gap, not just a long-lived history row.
    private static IEnumerable<ListeningReturn> ReturnsFor(IGrouping<Guid, ListeningSessionRow> group,
        Dictionary<Guid, ListeningHistoryRef> firstByFile, IReadOnlyList<RankedSong> songs)
    {
        DateTime? previous = firstByFile.GetValueOrDefault(group.Key)?.LastBeforePeriod;
        var ordered = group.OrderBy(r => r.StartedAt).ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            var current = ordered[index];

            if (previous is { } last
                && (current.StartedAt - last).TotalDays >= MinReturnGapDays
                && ListensOnTwoDaysFrom(ordered, index))
            {
                var song = songs.First(s => s.FileId == group.Key);

                yield return new ListeningReturn(group.Key, song.DisplayTitle, song.DisplayArtist, last,
                    current.StartedAt, (int)(current.StartedAt - last).TotalDays);
            }

            previous = current.StartedAt;
        }
    }

    private static bool ListensOnTwoDaysFrom(List<ListeningSessionRow> ordered, int index)
    {
        return ordered.Skip(index).Select(r => DateOnly.FromDateTime(r.StartedAt)).Distinct().Take(2).Count() >= 2;
    }

    private static double Median(IEnumerable<long> values)
    {
        var sorted = values.Order().ToArray();

        if (sorted.Length == 0) return 0;
        if (sorted.Length % 2 == 1) return sorted[sorted.Length / 2];

        return sorted[sorted.Length / 2 - 1] / 2.0 + sorted[sorted.Length / 2] / 2.0;
    }
}
