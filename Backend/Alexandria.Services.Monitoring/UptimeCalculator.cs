using Alexandria.Dto.Events.Operational;

namespace Alexandria.Services.Monitoring;

internal static class UptimeCalculator
{
    /// <summary>
    /// Percentage of the elapsed part of <c>[from, to)</c> not covered by any downtime interval.
    /// Overlapping incidents are merged so concurrent failures are counted once, and time after
    /// <paramref name="now"/> is excluded because it has not been observed yet.
    /// </summary>
    internal static double Calculate(IEnumerable<DowntimeInterval> intervals, DateTime from, DateTime to,
        DateTime now)
    {
        var windowEnd = to < now ? to : now;
        if (windowEnd <= from)
            return 100.0;

        var clipped = intervals
            .Select(i => (Start: Max(i.Start, from), End: Min(i.End ?? windowEnd, windowEnd)))
            .Where(i => i.End > i.Start)
            .OrderBy(i => i.Start)
            .ToList();

        var downtime = TimeSpan.Zero;
        DateTime? runStart = null;
        var runEnd = DateTime.MinValue;

        foreach (var (start, end) in clipped)
        {
            if (runStart is null)
            {
                runStart = start;
                runEnd = end;
                continue;
            }

            if (start <= runEnd)
            {
                runEnd = Max(runEnd, end);
                continue;
            }

            downtime += runEnd - runStart.Value;
            runStart = start;
            runEnd = end;
        }

        if (runStart is not null)
            downtime += runEnd - runStart.Value;

        var total = windowEnd - from;
        var uptime = 100.0 * (1 - downtime.TotalSeconds / total.TotalSeconds);

        return Math.Clamp(uptime, 0.0, 100.0);
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
