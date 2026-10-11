using Alexandria.Dto.Enrichment;

namespace Alexandria.Common.Services;

public interface IEnrichmentBackfillService
{
    /// <summary>
    /// Enriches matching files that predate the requested cutoff and lack successful
    /// enrichment, scoped to the live AutoTag rule's policy directory and inherited
    /// subtree. Missing/deleted rules or policies and disabled autotagging are skipped.
    /// </summary>
    Task<EnrichmentBackfillResult> BackfillRuleAsync(EnrichmentBackfillRequest request, CancellationToken ct = default);
}
