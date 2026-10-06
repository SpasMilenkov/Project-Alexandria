using Alexandria.Dto.Files.Streaming.Stats;

namespace Alexandria.Services.Streaming.Wrapped;

internal static class ListeningTimeline
{
    public static TimelineResponse Build(
        DateTime periodStart, DateTime periodEnd, IEnumerable<ListeningSessionRow> sessions)
    {
        var rows = sessions
            .Where(s => s.ListenedSeconds > 0 && s.StartedAt >= periodStart && s.StartedAt < periodEnd)
            .OrderBy(s => s.StartedAt).ThenBy(s => s.SessionId).ToList();

        var days = rows
            .GroupBy(s => DateOnly.FromDateTime(s.StartedAt.ToUniversalTime()))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var top = g
                    .GroupBy(s => s.FileId)
                    .Select(fg =>
                    {
                        var first = fg.First();

                        var artist = fg
                            .Where(s => !string.IsNullOrWhiteSpace(s.Artist))
                            .GroupBy(s => s.Artist!.Trim())
                            .OrderByDescending(ag => ag.Count())
                            .ThenBy(ag => ag.Min(s => s.StartedAt))
                            .Select(ag => ag.Key)
                            .FirstOrDefault();

                        return new TimelineDayTop(
                            ListeningStatsCompute.DisplayTitle(first.Title, first.FileName),
                            artist,
                            fg.Sum(s => s.ListenedSeconds));
                    })
                    .OrderByDescending(t => t.Seconds)
                    .ThenBy(t => t.Title, StringComparer.Ordinal)
                    .Take(3)
                    .ToList();

                return new TimelineDayPoint(
                    g.Key.ToString("yyyy-MM-dd"),
                    g.Sum(s => s.ListenedSeconds),
                    g.Count(s => s.IsQualifiedPlay()),
                    top)
                {
                    SessionCount = g.Count()
                };
            })
            .ToList();

        var months = rows
            .GroupBy(s => new DateOnly(s.StartedAt.ToUniversalTime().Year, s.StartedAt.ToUniversalTime().Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => new TimelineMonthPoint(
                g.Key.ToString("yyyy-MM-dd"),
                g.Sum(s => s.ListenedSeconds),
                g.Count(s => s.IsQualifiedPlay()))
            {
                SessionCount = g.Count()
            })
            .ToList();

        return new TimelineResponse(
            periodStart,
            periodEnd,
            days,
            months,
            rows.Sum(s => s.ListenedSeconds),
            rows.Count,
            days.Count)
        {
            QualifiedPlayCount = days.Sum(d => d.Plays)
        };
    }
}
