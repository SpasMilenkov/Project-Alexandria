namespace Alexandria.Common;

public static class ListeningPlayQualification
{
    public const double MinimumSeconds = 30;

    public static double Threshold(double? durationSeconds)
    {
        if (!durationSeconds.HasValue || !double.IsFinite(durationSeconds.Value) || durationSeconds.Value <= 0)
            return MinimumSeconds;

        return Math.Min(MinimumSeconds, durationSeconds.Value / 2);
    }

    public static bool IsQualified(long listenedSeconds, double? durationSeconds)
        => listenedSeconds > 0 && listenedSeconds >= Threshold(durationSeconds);
}
