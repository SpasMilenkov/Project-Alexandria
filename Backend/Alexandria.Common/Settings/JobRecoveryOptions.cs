namespace Alexandria.Common.Settings;

/// <summary>
/// Thresholds for the job recovery sweep, bound from the <c>Monitoring:JobRecovery</c>
/// configuration section.
/// </summary>
public sealed class JobRecoveryOptions
{
    public int SweepIntervalMinutes { get; set; } = 5;

    /// <summary>Maximum jobs republished per category in one sweep.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>A preview job Processing without a heartbeat for this long is failed as stalled.</summary>
    public int PreviewStalledMinutes { get; set; } = 30;

    public int LyricsStalledMinutes { get; set; } = 15;

    /// <summary>Generous because transcodes report no progress heartbeat while running.</summary>
    public int TranspilationStalledMinutes { get; set; } = 360;

    /// <summary>
    /// First automatic preview retry waits this long after the failure; each further attempt doubles it.
    /// </summary>
    public int PreviewRetryBaseDelayMinutes { get; set; } = 5;

    /// <summary>A preview job still Queued after this long is assumed to have lost its message.</summary>
    public int OrphanedQueuedMinutes { get; set; } = 30;
}
