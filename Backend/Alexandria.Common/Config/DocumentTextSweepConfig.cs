namespace Alexandria.Common.Config;

/// <summary>
/// Pacing for reading document body text. Bind from <c>appsettings.json</c> under the
/// <c>"DocumentTextSweep"</c> section.
/// </summary>
public class DocumentTextSweepConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Each document is downloaded whole, because <c>pdftotext</c> reads a local file.
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Pause between batches while there is work, so a large backfill leaves room for previews.
    /// </summary>
    public int BatchDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Pause once nothing is left, which is also how long a new upload waits to become
    /// searchable by its text.
    /// </summary>
    public int IdleDelaySeconds { get; set; } = 30;

    /// <summary>
    /// How long a document whose reading failed waits before it is tried again.
    /// </summary>
    public int RetryFailedAfterHours { get; set; } = 6;

    /// <summary>
    /// Failed attempts at one extractor version before a document is left alone until an admin
    /// retries it.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;
}