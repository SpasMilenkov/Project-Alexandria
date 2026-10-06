using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Stats;

namespace Alexandria.Services.Streaming.Wrapped;

internal static class ListeningRhythm
{
    private const int MinPlays = 10;
    private const int MinActiveDays = 5;
    private const double MinLeadingShare = 0.35;
    private const double MinLeadMargin = 0.1;

    public static WrappedRhythm Select(RawListeningStats stats)
    {
        var windows = SecondsByScene(stats);

        var ranked = windows.Select((seconds, index) => new { Seconds = seconds, Scene = (WrappedTimeScene)index })
            .OrderByDescending(x => x.Seconds).ThenBy(x => x.Scene).ToArray();

        var total = (double)Math.Max(1, stats.TotalListenedSeconds);
        var share = ranked[0].Seconds / total;
        var margin = (ranked[0].Seconds - ranked[1].Seconds) / total;

        if (stats.QualifiedPlayCount < MinPlays || stats.Insights.ActiveDays < MinActiveDays)
            return new WrappedRhythm(null, WrappedRhythmEvidence.Sparse, share, windows);

        if (share < MinLeadingShare || margin < MinLeadMargin)
            return new WrappedRhythm(null, WrappedRhythmEvidence.Balanced, share, windows);

        return new WrappedRhythm(ranked[0].Scene, WrappedRhythmEvidence.Pronounced, share, windows);
    }

    internal static WrappedTimeScene SceneForHour(int hour)
    {
        if (hour < 6) return WrappedTimeScene.Night;
        if (hour < 11) return WrappedTimeScene.Morning;
        if (hour < 14) return WrappedTimeScene.Midday;
        if (hour < 18) return WrappedTimeScene.Afternoon;

        return WrappedTimeScene.Evening;
    }

    private static long[] SecondsByScene(RawListeningStats stats)
    {
        var windows = new long[5];

        for (var hour = 0; hour < Math.Min(24, stats.SecondsByHourUtc.Count); hour++)
            windows[(int)SceneForHour(hour)] += stats.SecondsByHourUtc[hour];

        return windows;
    }
}
