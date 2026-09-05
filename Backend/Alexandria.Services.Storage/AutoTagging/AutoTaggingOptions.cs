namespace Alexandria.Services.Storage.AutoTagging;

/// <summary>
/// Options for the auto-tagging derivation layer, bound from the <c>Tagging</c>
/// configuration section (wired in Phase 5). Values here are the locked defaults.
/// </summary>
public sealed class AutoTaggingOptions
{
    public GenreTaggingOptions Genre { get; set; } = new();

    public MoodTaggingOptions Mood { get; set; } = new();

    /// <summary>
    /// Ordered, model-agnostic analyzer list used to pick the winner per facet
    /// (higher priority first). The sync re-derives authority from the file's
    /// current enrichment rows each run, so a weaker model can never regress a
    /// stronger one's verdict.
    /// </summary>
    public string[] ModelPriority { get; set; } = ["maest", "effnet"];

    /// <summary>
    /// How often the auto-tag sweeper runs (backstop + failed-retry). The sweeper is a
    /// safety net only — normal tagging happens in-process right after enrichment commits.
    /// Dev override: 30s; production default: 15min.
    /// </summary>
    public int AutoTagSyncIntervalSeconds { get; set; } = 900;

    /// <summary>
    /// Minimum time between enrichment attempts for a file before the sweeper will
    /// re-queue it. The latest attempt (any outcome, via the linked <c>Job.CompletedAt</c>)
    /// must be older than this for the file to be a retry candidate.
    /// </summary>
    public int RetryCooldownHours { get; set; } = 24;
}

public sealed class GenreTaggingOptions
{
    /// <summary>Number of genre predictions kept per file (top-N by score).</summary>
    public int TopN { get; set; } = 3;

    /// <summary>Minimum score for a genre prediction to become a candidate.</summary>
    public double ConfidenceThreshold { get; set; } = 0.1;
}

public sealed class MoodTaggingOptions
{
    /// <summary>
    /// Minimum winning-pole score per mood axis to emit a candidate. Near-50/50 splits
    /// (neither pole above the floor) tag neither pole.
    /// </summary>
    public double ConfidenceThreshold { get; set; } = 0.65;
}