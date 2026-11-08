namespace Alexandria.Common.Config;

/// <summary>
/// Pacing for the capture metadata backfill. Bind from <c>appsettings.json</c> under the
/// <c>"CaptureMetadataSweep"</c> section.
/// </summary>
public class CaptureMetadataSweepConfig
{
    public bool Enabled { get; set; } = true;

    public int BatchSize { get; set; } = 20;

    /// <summary>
    /// Pause between batches while there is work, so a large library never competes with
    /// preview jobs for long.
    /// </summary>
    public int BatchDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Pause once nothing is left, or when a whole batch failed and storage may be down.
    /// </summary>
    public int IdleDelaySeconds { get; set; } = 600;

    /// <summary>
    /// How long a version that failed stays out of the sweep before it is tried again.
    /// </summary>
    public int RetryFailedAfterHours { get; set; } = 24;
}
