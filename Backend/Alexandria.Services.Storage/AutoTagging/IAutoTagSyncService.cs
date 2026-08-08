namespace Alexandria.Services.Storage.AutoTagging;

/// <summary>
/// Applies auto-tags to a single file from its current enrichment rows. Resolves the
/// owner's <c>AllowAutoTagRegression</c> preference, picks the authoritative row per facet
/// from <see cref="AutoTaggingOptions.ModelPriority"/>, derives candidates and applies them.
/// </summary>
public interface IAutoTagSyncService
{
    /// <summary>
    /// Syncs auto-tags for <paramref name="fileId"/>. No-ops (without error) when the file
    /// is missing/deleted, has no enrichment rows, has no authoritative (non-failure) rows,
    /// or derives no candidates. Never throws for per-file data conditions.
    /// </summary>
    Task SyncFileAsync(Guid fileId, CancellationToken ct = default);
}