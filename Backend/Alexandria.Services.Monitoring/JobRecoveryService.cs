using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Data.Models.Enumerators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Monitoring;

/// <summary>
/// Database-driven recovery for jobs whose broker message is gone or whose worker died:
/// stalled Processing jobs are failed so they become claimable again, failed previews are
/// retried with backoff, and previews stuck in Queued are republished.
/// </summary>
public class JobRecoveryService(
    IUnitOfWork unitOfWork,
    IPreviewService previewService,
    IOptions<JobRecoveryOptions> options,
    ILogger<JobRecoveryService> logger)
{
    internal const string StalledError = "stalled: no progress within the recovery threshold";
    internal const string SourceGoneError = "preview source is no longer available";
    internal const string PublishFailedError = "publish failed";

    private static readonly JobType[] PreviewTypes = [JobType.MediaPreview, JobType.DocumentPreview];

    public async Task SweepAsync(DateTime now, CancellationToken ct)
    {
        await FailStalledJobsAsync(now, ct);
        await RetryFailedPreviewsAsync(now, ct);
        await RepublishOrphanedPreviewsAsync(now, ct);
    }

    internal async Task FailStalledJobsAsync(DateTime now, CancellationToken ct)
    {
        var config = options.Value;
        var thresholds = new (JobType Type, int Minutes)[]
        {
            (JobType.MediaPreview, config.PreviewStalledMinutes),
            (JobType.DocumentPreview, config.PreviewStalledMinutes),
            (JobType.LyricsFetch, config.LyricsStalledMinutes),
            (JobType.Transpilation, config.TranspilationStalledMinutes)
        };

        foreach (var (type, minutes) in thresholds)
        {
            var cutoff = now - TimeSpan.FromMinutes(Math.Max(minutes, 1));
            var failed = await unitOfWork.Jobs.FailStalledJobsAsync(type, cutoff, StalledError, ct);

            if (failed > 0)
                logger.LogWarning("Failed {Count} stalled {JobType} jobs", failed, type);
        }
    }

    internal async Task RetryFailedPreviewsAsync(DateTime now, CancellationToken ct)
    {
        var config = options.Value;
        var baseDelay = TimeSpan.FromMinutes(Math.Max(config.PreviewRetryBaseDelayMinutes, 1));
        var candidates = await unitOfWork.Jobs.GetRetryCandidatesAsync(
            PreviewTypes, now - baseDelay, Math.Max(config.BatchSize, 1), ct);

        foreach (var job in candidates)
        {
            if (job.CompletedAt + RetryDelay(baseDelay, job.RetryCount) > now)
                continue;

            if (!await unitOfWork.Jobs.TryRequeueForRetryAsync(job.Id, ct))
                continue;

            await RepublishAsync(job.Id, ct);
        }
    }

    internal async Task RepublishOrphanedPreviewsAsync(DateTime now, CancellationToken ct)
    {
        var config = options.Value;
        var cutoff = now - TimeSpan.FromMinutes(Math.Max(config.OrphanedQueuedMinutes, 1));
        var orphans = await unitOfWork.Jobs.GetOrphanedQueuedJobsAsync(
            PreviewTypes, cutoff, Math.Max(config.BatchSize, 1), ct);

        foreach (var job in orphans)
        {
            // Restarts the orphan clock so a message still sitting in a busy queue is not
            // republished on every sweep; duplicate deliveries lose the claim and are acked.
            await unitOfWork.Jobs.TouchAsync(job.Id, ct);
            await RepublishAsync(job.Id, ct);
        }

        if (orphans.Count > 0)
            logger.LogInformation("Republished {Count} orphaned queued preview jobs", orphans.Count);
    }

    internal static TimeSpan RetryDelay(TimeSpan baseDelay, int retryCount)
        => baseDelay * Math.Pow(2, Math.Clamp(retryCount, 0, 10));

    private async Task RepublishAsync(Guid jobId, CancellationToken ct)
    {
        try
        {
            if (await previewService.RepublishJobAsync(jobId, ct))
                return;

            await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Failed, errorDetail: SourceGoneError, ct: ct);
            await unitOfWork.Jobs.MarkNonRetryableAsync(jobId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Republishing preview job {JobId} failed", jobId);
            await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Failed, errorDetail: PublishFailedError, ct: ct);
        }
    }
}
