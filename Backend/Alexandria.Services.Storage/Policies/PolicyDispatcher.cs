using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Helpers;
using Alexandria.Common.Policies;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Policies;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Policies;

public sealed partial class PolicyDispatcher(
    IUnitOfWork unitOfWork,
    IJobQueue jobQueue,
    ILogger<PolicyDispatcher> logger)
    : IPolicyDispatcher
{
    public async Task DispatchAsync(FileFinalizedEvent ev, CancellationToken ct = default)
    {
        if (ev.DirectoryId is null) return;

        DirectoryPolicyDto? policy;

        try
        {
            policy = await unitOfWork.DirectoryPolicies
                .ResolveEffectivePolicyAsync(ev.DirectoryId.Value, ct);
        }
        catch (Exception ex)
        {
            LogPolicyResolutionFailed(logger, ex, ev.DirectoryId.Value, ev.FileId);
            return;
        }

        if (policy is null) return;

        foreach (var rule in policy.Rules.OrderBy(r => r.Priority))
        {
            if (ev.IsNewVersion && !rule.ApplyOnNewVersion) continue;
            if (!PolicyRuleTrigger.Matches(rule.TriggerType, rule.TriggerValue, ev.MimeType)) continue;

            // Older backup rules are kept, but there is no backup job to run them yet
            if (rule.ActionType == PolicyActionType.Backup)
            {
                LogBackupRuleSkipped(logger, rule.Id, ev.FileId);
                continue;
            }

            if (rule.ActionType == PolicyActionType.Transcode && !TranscodeSupportedFileTypes.IsSupported(ev.MimeType))
            {
                LogTranscodeRuleSkipped(logger, rule.Id, ev.FileId, ev.MimeType);
                continue;
            }

            try
            {
                var parameters = PolicyRuleParametersResolver.ResolveFromDto(rule);

                await (parameters switch
                {
                    TranscodeParameters p => jobQueue.QueueTranspilationJobAsync(
                        ev.VersionId ?? throw new InvalidOperationException(), ev.FileId, ev.OwnerId, p, ct),
                    BackupParameters p => jobQueue.QueueBackupAsync(ev.FileId, p, ct),
                    AutoTagParameters p => jobQueue.QueueAutoTagAsync(ev.FileId, p, ev.MimeType, ct),
                    _ => Task.CompletedTask
                });

                LogJobQueued(logger, rule.ActionType.ToString(), ev.FileId, rule.Id);
            }
            catch (MessageBrokerUnavailableException ex)
            {
                LogRuleDispatchSkipped(logger, rule.Id, ev.FileId, ex.Message);
            }
            catch (Exception ex)
            {
                LogRuleDispatchFailed(logger, ex, rule.Id, ev.FileId);
            }
        }
    }
}
