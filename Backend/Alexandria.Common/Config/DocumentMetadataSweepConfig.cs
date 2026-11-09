namespace Alexandria.Common.Config;

/// <summary>
/// Pacing for the document metadata backfill. Bind from <c>appsettings.json</c> under the
/// <c>"DocumentMetadataSweep"</c> section.
/// </summary>
public class DocumentMetadataSweepConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Each document is downloaded whole, because <c>pdfinfo</c> reads a local file, so batches
    /// stay smaller than the image backfill's.
    /// </summary>
    public int BatchSize { get; set; } = 10;

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
