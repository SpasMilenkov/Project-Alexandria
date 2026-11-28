using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Policies;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Enrichment;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Storage.Policies;

public sealed partial class EnrichmentBackfillService(
    IUnitOfWork unitOfWork,
    IOutbox outbox,
    ILogger<EnrichmentBackfillService> logger,
    IOptions<ProcessingCapabilities> capabilities) : IEnrichmentBackfillService
{
    private readonly bool _autoTaggingEnabled = capabilities.Value.AutoTaggingActive;

    public async Task<EnrichmentBackfillResult> BackfillRuleAsync(EnrichmentBackfillRequest request,
        CancellationToken ct = default)
    {
        var policyId = request.PolicyId;
        var policy = await unitOfWork.DirectoryPolicies.GetByIdAsync(policyId, ct);
        if (policy is null)
        {
            LogPolicyNotFound(logger, policyId);
            return new EnrichmentBackfillResult(0, 0, 0);
        }

        var rule = policy.Rules.FirstOrDefault(r => r.Id == request.RuleId
            && r.DeletedAt == null && r.ActionType == PolicyActionType.AutoTag);

        if (rule is null)
        {
            LogRuleNotFound(logger, request.RuleId, policyId);
            return new EnrichmentBackfillResult(0, 0, 0);
        }

        if (!_autoTaggingEnabled)
        {
            LogBackfillSkippedDisabled(logger, policyId);
            return new EnrichmentBackfillResult(0, 0, 0);
        }

        var directoryIds = await PolicyScope.ResolveGovernedDirectoryIdsAsync(unitOfWork, policy, ct);

        var candidates = await unitOfWork.EssentiaBatchFiles
            .GetEnrichmentBackfillCandidatesAsync(directoryIds, request.Cutoff.UtcDateTime, ct);

        // Per-file triggers reuse the shared guard: unsupported MIME types, files that gained a
        // successful enrichment since candidate selection and files still being analysed are
        // skipped, so the sweep is idempotent and safe to re-run. One transaction for the whole
        // rule, so a failure part way leaves nothing enqueued and the redelivery starts clean.
        var published = await unitOfWork.InTransactionAsync(async () =>
        {
            var count = 0;

            foreach (var candidate in candidates)
            {
                if (!PolicyRuleTrigger.Matches(rule.TriggerType, rule.TriggerValue, candidate.MimeType))
                    continue;

                if (await AutoTagTrigger.QueueIfNeededAsync(
                        outbox, unitOfWork, _autoTaggingEnabled,
                        candidate.FileId, candidate.MimeType, logger, ct))
                    count++;
            }

            return count;
        }, ct);

        LogBackfillCompleted(logger, policyId, candidates.Count, published);
        return new EnrichmentBackfillResult(candidates.Count, published, candidates.Count - published);
    }
}
