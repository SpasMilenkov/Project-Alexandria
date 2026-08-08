namespace Alexandria.Common.Settings;

/// <summary>
/// Options for the audio-analysis monitoring endpoints, bound from the
/// <c>Monitoring:Enrichment</c> configuration section. Values not present in config fall
/// back to the locked defaults.
/// </summary>
public sealed class EnrichmentMonitoringOptions
{
    /// <summary>
    /// A batch-file still <c>Pending</c> inside a dispatched batch is reported as stuck when
    /// it has been pending for at least this many minutes.
    /// </summary>
    public int StuckThresholdMinutes { get; set; } = 30;
}