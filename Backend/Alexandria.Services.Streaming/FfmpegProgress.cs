using System.Globalization;

namespace Alexandria.Services.Streaming;

/// <summary>
/// Follows one ffmpeg run's position from the <c>key=value</c> lines its <c>-progress</c> option
/// writes to standard output. Lines arrive on the thread reading the output while the job's
/// watchdog asks for the percentage, so the position is shared through atomic reads and writes.
/// </summary>
internal sealed class FfmpegProgress
{
    // Polls come every few seconds, so blocks written more often would only be thrown away.
    public const string Arguments = "-progress pipe:1 -stats_period 5";

    private const string PositionKey = "out_time_us=";

    // Finished means published, which only the job handler knows; a run tops out just below.
    private const int MaxRunningPercent = 99;

    private long _microseconds = -1;

    public void Read(string line)
    {
        // Before the first frame the position is N/A, which does not parse and is skipped.
        if (line.StartsWith(PositionKey, StringComparison.Ordinal)
            && long.TryParse(line.AsSpan(PositionKey.Length), NumberStyles.None, CultureInfo.InvariantCulture,
                out var microseconds))
            Interlocked.Exchange(ref _microseconds, microseconds);
    }

    /// <summary>
    /// Returns null until ffmpeg has reported a position.
    /// </summary>
    public int? Percent(TimeSpan duration)
    {
        var microseconds = Interlocked.Read(ref _microseconds);

        if (microseconds < 0 || duration <= TimeSpan.Zero)
            return null;

        var percent = (int)(TimeSpan.FromMicroseconds(microseconds) / duration * 100);

        return Math.Clamp(percent, 0, MaxRunningPercent);
    }
}
