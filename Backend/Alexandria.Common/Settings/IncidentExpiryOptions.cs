namespace Alexandria.Common.Settings;

/// <summary>
/// Idle windows after which incidents that no producer resolves on its own are closed
/// automatically, bound from the <c>Monitoring:IncidentExpiry</c> configuration section.
/// </summary>
public sealed class IncidentExpiryOptions
{
    /// <summary>
    /// A worker-cycle failure resolves once the worker has not failed again for this many minutes.
    /// </summary>
    public int WorkerCycleFailureIdleMinutes { get; set; } = 30;

    /// <summary>
    /// A user-reported problem resolves after this many hours without admin action.
    /// </summary>
    public int UserReportIdleHours { get; set; } = 24;

    /// <summary>
    /// How often the expiry sweep runs.
    /// </summary>
    public int SweepIntervalMinutes { get; set; } = 5;
}
