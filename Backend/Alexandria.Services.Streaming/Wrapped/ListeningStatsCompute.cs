using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Stats;

namespace Alexandria.Services.Streaming.Wrapped;

/// <summary>
/// Pure in-memory compute for listening stats. Single pass over the
/// period's session rows plus the lightweight whole-history refs; every metric
/// in the spec catalog comes out as raw numbers with no narrative decisions.
/// Artist keys reuse the exact normalized match from auto-playlists;
/// display names are the mode raw string with first-seen tie-break.
/// </summary>
internal static class ListeningStatsCompute
{
    public static string? NormalizeArtist(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    public static TimeOfDayBucket BucketForHour(int hour)
    {
        if (hour < 6) return TimeOfDayBucket.Night;
        if (hour < 12) return TimeOfDayBucket.Morning;
        if (hour < 18) return TimeOfDayBucket.Afternoon;

        return TimeOfDayBucket.Evening;
    }

    public static string DisplayTitle(string? title, string fileName)
    {
        return string.IsNullOrWhiteSpace(title) ? fileName : title.Trim();
    }

    public static RawListeningStats Compute(
        DateTime periodStart,
        DateTime periodEnd,
        IEnumerable<ListeningSessionRow> sessions,
        IEnumerable<ListeningHistoryRef> histories)
    {
        var rows = sessions.Where(s => s.ListenedSeconds > 0 && s.StartedAt >= periodStart && s.StartedAt < periodEnd)
            .OrderBy(s => s.StartedAt).ThenBy(s => s.SessionId).ToList();

        var refs = histories.OrderBy(h => h.HistoryCreatedAt).ThenBy(h => h.FileId).ToList();

        var (discoveryCount, newArtists) = Discoveries(refs, periodStart, periodEnd);

        if (rows.Count == 0) return EmptyStats(discoveryCount, newArtists);

        var totals = new ListeningSessionTotals();

        foreach (var row in rows) totals.Add(row);

        var topArtists = RankArtists(totals.Artists);
        var topSongs = RankSongs(totals.Songs);
        var mostReplayed = MostReplayed(totals.Songs, rows);
        var mostMinutes = MostMinutes(totals.Songs);

        var dominantBucket = (TimeOfDayBucket)Array.IndexOf(totals.SecondsByBucket, totals.SecondsByBucket.Max());

        var (streakLength, streakStart, streakEnd) = LongestRun(totals.SecondsByDay.Keys.Order().ToList());

        var busiestDay = totals.SecondsByDay
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .First();

        var longest = totals.LongestRow!;
        var first = totals.FirstRow!;
        var last = totals.LastRow!;

        return new RawListeningStats(
            true,
            totals.TotalSeconds,
            rows.Count,
            topArtists,
            topSongs,
            mostReplayed,
            mostMinutes,
            totals.SecondsByHour,
            dominantBucket,
            totals.WeekdaySeconds,
            totals.WeekendSeconds,
            streakLength,
            streakStart,
            streakEnd,
            busiestDay.Key,
            busiestDay.Value,
            new LongestSitting(longest.SessionId, longest.FileId, longest.ListenedSeconds,
                longest.StartedAt.ToUniversalTime()),
            new BookendSession(first.FileId, first.StartedAt.ToUniversalTime()),
            new BookendSession(last.FileId, last.StartedAt.ToUniversalTime()),
            discoveryCount,
            newArtists,
            (double)totals.CompletedSessions / rows.Count,
            totals.CompletedSessions)
        {
            QualifiedPlayCount = totals.QualifiedPlayCount,
            Insights = ListeningStatsInsights.Build(periodStart, periodEnd, rows, refs, topArtists, topSongs,
                totals.SecondsByBucket)
        };
    }

    private static RawListeningStats EmptyStats(int discoveryCount, IReadOnlyList<DiscoveredArtist> newArtists)
    {
        return new RawListeningStats(
            false, 0, 0, [], [], null, null,
            new long[24], TimeOfDayBucket.Night, 0, 0,
            0, null, null, null, 0, null, null, null,
            discoveryCount, newArtists, 0, 0);
    }

    private static List<RankedArtist> RankArtists(Dictionary<string, ArtistAccumulator> artists)
    {
        return artists
            .OrderByDescending(kv => kv.Value.TotalSeconds)
            .ThenByDescending(kv => kv.Value.QualifiedPlayCount)
            .ThenBy(kv => kv.Value.FirstSeenPosition)
            .Select(kv => new RankedArtist(
                kv.Key, kv.Value.DisplayName, kv.Value.TotalSeconds, kv.Value.SessionCount,
                kv.Value.QualifiedPlayCount))
            .ToList();
    }

    private static IOrderedEnumerable<KeyValuePair<Guid, SongAccumulator>> ByListeningTime(
        Dictionary<Guid, SongAccumulator> songs)
    {
        return songs
            .OrderByDescending(kv => kv.Value.TotalSeconds)
            .ThenByDescending(kv => kv.Value.QualifiedPlayCount)
            .ThenBy(kv => kv.Value.FirstSeenPosition);
    }

    private static List<RankedSong> RankSongs(Dictionary<Guid, SongAccumulator> songs)
    {
        return ByListeningTime(songs)
            .Select(kv => new RankedSong(
                kv.Key, kv.Value.Title, kv.Value.DisplayArtist,
                kv.Value.TotalSeconds, kv.Value.SessionCount, kv.Value.QualifiedPlayCount))
            .ToList();
    }

    private static DurationExtreme MostMinutes(Dictionary<Guid, SongAccumulator> songs)
    {
        return ByListeningTime(songs)
            .Select(kv => new DurationExtreme(kv.Key, kv.Value.TotalSeconds))
            .First();
    }

    private static ReplayExtreme? MostReplayed(Dictionary<Guid, SongAccumulator> songs,
        List<ListeningSessionRow> rows)
    {
        var replayed = songs
            .Where(kv => kv.Value.QualifiedPlayCount > 0)
            .OrderByDescending(kv => kv.Value.QualifiedPlayCount)
            .ThenByDescending(kv => kv.Value.TotalSeconds)
            .ThenBy(kv => kv.Value.FirstSeenPosition)
            .Select(kv => new ReplayExtreme(kv.Key, kv.Value.QualifiedPlayCount))
            .FirstOrDefault();

        if (replayed is null) return null;

        return replayed with
        {
            ActiveDays = rows.Where(r => r.FileId == replayed.FileId && r.IsQualifiedPlay())
                .Select(r => DateOnly.FromDateTime(r.StartedAt)).Distinct().Count()
        };
    }

    private static (int Count, IReadOnlyList<DiscoveredArtist> NewArtists) Discoveries(
        List<ListeningHistoryRef> refs, DateTime periodStart, DateTime periodEnd)
    {
        var inRange = 0;
        var firstSeen = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        var displays = new Dictionary<string, DisplayVotes>(StringComparer.Ordinal);
        var votePosition = 0;

        foreach (var history in refs)
        {
            if (history.HistoryCreatedAt >= periodStart && history.HistoryCreatedAt < periodEnd)
                inRange++;

            var key = NormalizeArtist(history.Artist);

            if (key is null)
                continue;

            if (!firstSeen.TryGetValue(key, out var seen) || history.HistoryCreatedAt < seen)
                firstSeen[key] = history.HistoryCreatedAt;

            if (!displays.TryGetValue(key, out var votes))
            {
                votes = new DisplayVotes();
                displays[key] = votes;
            }

            votes.Vote(history.Artist!.Trim(), votePosition++);
        }

        var discovered = firstSeen
            .Where(kv => kv.Value >= periodStart && kv.Value < periodEnd)
            .OrderBy(kv => kv.Value)
            .ThenBy(kv => displays[kv.Key].FirstSeenPosition)
            .Select(kv => new DiscoveredArtist(kv.Key, displays[kv.Key].Pick()))
            .ToList();

        return (inRange, discovered);
    }

    private static (int Length, DateOnly? Start, DateOnly? End) LongestRun(List<DateOnly> orderedDays)
    {
        var bestLength = 0;
        DateOnly? bestStart = null;
        DateOnly? bestEnd = null;
        var runLength = 0;
        DateOnly? runStart = null;
        DateOnly? previous = null;

        foreach (var day in orderedDays)
        {
            if (previous.HasValue && day.DayNumber == previous.Value.DayNumber + 1)
            {
                runLength++;
            }
            else
            {
                runLength = 1;
                runStart = day;
            }

            if (runLength > bestLength)
            {
                bestLength = runLength;
                bestStart = runStart;
                bestEnd = day;
            }

            previous = day;
        }

        return (bestLength, bestStart, bestEnd);
    }
}
