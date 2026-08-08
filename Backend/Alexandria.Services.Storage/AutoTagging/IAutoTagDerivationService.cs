using Alexandria.Data.Models;
using Alexandria.Dto.Autotag;

namespace Alexandria.Services.Storage.AutoTagging;

/// <summary>
/// Derives auto-tag candidates from the file's current enrichment rows. Callers pass the
/// rows already selected as authoritative per facet (see the ModelPriority winner rule);
/// the service loads the seeded taxonomy, maps model keys to tags and reports drift.
/// </summary>
public interface IAutoTagDerivationService
{
    /// <summary>
    /// Derives the union of genre + mood candidates for the given enrichment rows.
    /// Enrichment rows for analyzers outside the auto-tagging set (e.g. failure rows) are
    /// ignored. Candidates are deduplicated by tag, keeping the highest confidence.
    /// </summary>
    Task<IReadOnlyList<TagCandidate>> DeriveAsync(
        IReadOnlyCollection<FileEnrichment> enrichmentRows,
        CancellationToken ct = default);
}