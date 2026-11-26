using System.Diagnostics;

namespace Alexandria.Services.Streaming;

/// <summary>
/// The time all ffmpeg runs of one job share. A job can run ffmpeg several times (encoder
/// fallbacks, a capped re-encode), while the job recovery stall threshold and the broker's
/// consumer timeout both count the job as a whole, so each run gets what the earlier ones left.
/// </summary>
internal sealed class TranscodeBudget(TimeSpan total)
{
    private readonly long _started = Stopwatch.GetTimestamp();

    public TimeSpan Total { get; } = total;

    public TimeSpan Remaining => Total - Stopwatch.GetElapsedTime(_started);
}
