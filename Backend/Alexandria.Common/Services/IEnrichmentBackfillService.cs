using Alexandria.Dto.Enrichment;

namespace Alexandria.Common.Services;

public interface IEnrichmentBackfillService
{
    /// <summary>
    /// Retroactively enriches files that predate <paramref name="policyId"/> and lack a
    /// successful enrichment job, scoped to the policy's directory (+ subtree when
    /// inherited). Each file goes through the normal enrich trigger path; completion
    /// reuses the existing results-consumer → auto-tag flow. Missing/deleted policies
    /// and a disabled autotagging flag yield an empty result, never an error.
    /// </summary>
    Task<EnrichmentBackfillResult> BackfillPolicyAsync(Guid policyId, CancellationToken ct = default);
}