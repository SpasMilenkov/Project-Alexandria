using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Dto.Enrichment;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Policies;

public sealed partial class EnrichmentBackfillService(
    IUnitOfWork unitOfWork,
    IPublisherService publisher,
    IConfiguration configuration,
    ILogger<EnrichmentBackfillService> logger) : IEnrichmentBackfillService
{
    private readonly bool _autoTaggingEnabled =
        bool.TryParse(configuration["Features:Autotagging"], out var enabled) && enabled;

    public async Task<EnrichmentBackfillResult> BackfillPolicyAsync(Guid policyId,
        CancellationToken ct = default)
    {
        var policy = await unitOfWork.DirectoryPolicies.GetByIdAsync(policyId, ct);
        if (policy is null)
        {
            LogPolicyNotFound(logger, policyId);
            return new EnrichmentBackfillResult(0, 0, 0);
        }

        if (!_autoTaggingEnabled)
        {
            LogBackfillSkippedDisabled(logger, policyId);
            return new EnrichmentBackfillResult(0, 0, 0);
        }

        var directoryIds = new List<Guid> { policy.DirectoryId };
        if (policy.InheritedByChildren)
        {
            var subdirectories =
                await unitOfWork.Directories.GetAllSubDirectoriesAsync(policy.DirectoryId, ct);
            directoryIds.AddRange(subdirectories.Select(d => d.Id));
        }

        var candidates = await unitOfWork.EssentiaBatchFiles
            .GetEnrichmentBackfillCandidatesAsync(directoryIds, policy.CreatedAt, ct);

        // Per-file triggers reuse the shared guard: unsupported MIME types and files
        // that gained a successful enrichment since candidate selection are skipped,
        // so the sweep is idempotent and safe to re-run.
        var published = 0;
        foreach (var candidate in candidates)
        {
            if (await AutoTagTrigger.QueueIfNeededAsync(
                    publisher, unitOfWork, _autoTaggingEnabled,
                    candidate.FileId, candidate.MimeType, logger, ct))
                published++;
        }

        LogBackfillCompleted(logger, policyId, candidates.Count, published);
        return new EnrichmentBackfillResult(candidates.Count, published, candidates.Count - published);
    }
}