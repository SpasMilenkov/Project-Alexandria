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
    /// retries it. Read it through <see cref="AttemptLimit"/>. The API reports this limit as well,
    /// so a value set for the document worker must be set for the API too.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// <see cref="MaxAttempts"/> as the sweep applies it: every document gets at least one try.
    /// The worker and the admin overview both read this, so they agree on what a failure's
    /// remaining tries are.
    /// </summary>
    public int AttemptLimit => Math.Max(MaxAttempts, 1);
}