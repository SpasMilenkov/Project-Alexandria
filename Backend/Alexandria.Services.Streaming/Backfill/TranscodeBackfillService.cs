using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Common.Helpers;
using Alexandria.Common.Policies;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Streaming.Backfill;

public sealed partial class TranscodeBackfillService(
    IUnitOfWork unitOfWork,
    ITranspilationJobService jobService,
    ILogger<TranscodeBackfillService> logger,
    IOptions<ProcessingCapabilities> capabilities) : ITranscodeBackfillService
{
    private static readonly TranscodeBackfillResult Nothing = new()
    {
        Candidates = 0,
        Created = 0,
        Requeued = 0,
        Skipped = 0,
    };

    public async Task<TranscodeBackfillResult> BackfillRuleAsync(TranscodeBackfillRequest request,
        CancellationToken ct = default)
    {
        if (!capabilities.Value.AdaptiveStreaming)
        {
            LogStreamingDisabled(logger, request.PolicyId);
            return Nothing;
        }

        var policy = await unitOfWork.DirectoryPolicies.GetByIdAsync(request.PolicyId, ct);
        if (policy is null)
        {
            LogPolicyNotFound(logger, request.PolicyId);
            return Nothing;
        }

        var rule = policy.Rules.FirstOrDefault(r => r.Id == request.RuleId
            && r.DeletedAt == null && r.ActionType == PolicyActionType.Transcode);

        if (rule is null)
        {
            LogRuleNotFound(logger, request.RuleId, request.PolicyId);
            return Nothing;
        }

        var parameters = (TranscodeParameters)PolicyRuleParametersResolver.Resolve(rule);
        var directoryIds = await PolicyScope.ResolveGovernedDirectoryIdsAsync(unitOfWork, policy, ct);

        var candidates = await unitOfWork.TranspilationJobs
            .GetTranscodeBackfillCandidatesAsync(directoryIds, request.Cutoff.UtcDateTime, ct);

        var created = 0;
        var requeued = 0;

        // Each file commits on its own rather than in one transaction for the rule: a large library
        // shouldn't hold one open for the whole sweep, and a redelivery after a crash finds the
        // files already handled running or covered and skips them.
        foreach (var candidate in candidates)
        {
            if (!PolicyRuleTrigger.Matches(rule.TriggerType, rule.TriggerValue, candidate.MimeType))
                continue;

            if (!TranscodeSupportedFileTypes.IsSupported(candidate.MimeType))
                continue;

            var step = TranscodeBackfillPlanner.Plan(parameters, candidate.Transcode);

            switch (step.Action)
            {
                case TranscodeBackfillAction.Create:
                    if (await TryCreateAsync(candidate, parameters, ct)) created++;
                    break;
                case TranscodeBackfillAction.Requeue:
                    if (await TryRequeueAsync(candidate, step, ct)) requeued++;
                    break;
            }
        }

        LogBackfillCompleted(logger, request.RuleId, request.PolicyId, candidates.Count, created, requeued);

        return new TranscodeBackfillResult
        {
            Candidates = candidates.Count,
            Created = created,
            Requeued = requeued,
            Skipped = candidates.Count - created - requeued,
        };
    }

    private async Task<bool> TryCreateAsync(TranscodeBackfillCandidate candidate, TranscodeParameters parameters,
        CancellationToken ct)
    {
        try
        {
            await jobService.CreateJobAsync(candidate.VersionId, candidate.OwnerId,
                parameters.AudioRungs, parameters.VideoRungs, ct);
            return true;
        }
        catch (TranspilationJobConflictException)
        {
            // An upload or another sweep queued it after the candidates were read
            LogAlreadyInFlight(logger, candidate.FileId);
            return false;
        }
        catch (ContentObjectNotFoundException)
        {
            // Its content was orphaned or deleted after the candidates were read. Skipping it keeps
            // one file from failing the sweep, and every redelivery with it, for the rest of the library.
            LogContentMissing(logger, candidate.FileId, candidate.VersionId);
            return false;
        }
    }

    private async Task<bool> TryRequeueAsync(TranscodeBackfillCandidate candidate, TranscodeBackfillStep step,
        CancellationToken ct)
    {
        var transcode = candidate.Transcode!;

        try
        {
            await jobService.UpdateStatusAsync(transcode.TranspilationId, transcode.UserId, JobStatus.Queued,
                step.AudioRungs, step.VideoRungs, ct);
            return true;
        }
        catch (Exception ex) when (ex is InvalidTranspilationTransitionException or TranspilationJobNotFoundException)
        {
            // The job was picked up or removed after the candidates were read
            LogAlreadyInFlight(logger, candidate.FileId);
            return false;
        }
    }
}
