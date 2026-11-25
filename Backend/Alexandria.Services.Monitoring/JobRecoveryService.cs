using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Data.Models.Enumerators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Monitoring;

/// <summary>
/// Database-driven recovery for jobs whose worker died or whose work failed: stalled Processing
/// jobs are failed so they become claimable again, and failed previews are retried with backoff.
/// Lost messages need no recovery here, since every job message goes through the outbox.
/// </summary>
public class JobRecoveryService(
    IUnitOfWork unitOfWork,
    IPreviewService previewService,
    IMessageBrokerStatus brokerStatus,
    IOptions<JobRecoveryOptions> options,
    ILogger<JobRecoveryService> logger)
{
    internal const string StalledError = "stalled: no progress within the recovery threshold";
    internal const string SourceGoneError = "preview source is no longer available";

    private static readonly JobType[] PreviewTypes = [JobType.MediaPreview, JobType.DocumentPreview];

    public async Task SweepAsync(DateTime now, CancellationToken ct)
    {
        await FailStalledJobsAsync(now, ct);

        // Without a broker a retried job would sit in Queued with nothing to send it. A broker
        // that is only down is fine: the outbox holds the message until it is back.
        if (!brokerStatus.IsEnabled)
            return;

        await RetryFailedPreviewsAsync(now, ct);
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

            await RetryAsync(job.Id, ct);
        }
    }

    internal static TimeSpan RetryDelay(TimeSpan baseDelay, int retryCount)
        => baseDelay * Math.Pow(2, Math.Clamp(retryCount, 0, 10));

    // The requeue and its message commit together, so a failure leaves the job Failed for the
    // next sweep instead of Queued with no message behind it.
    private async Task RetryAsync(Guid jobId, CancellationToken ct)
    {
        try
        {
            await unitOfWork.InTransactionAsync(async () =>
            {
                if (!await unitOfWork.Jobs.TryRequeueForRetryAsync(jobId, ct))
                    return;

                if (await previewService.RepublishJobAsync(jobId, ct))
                    return;

                await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Failed, errorDetail: SourceGoneError, ct: ct);
                await unitOfWork.Jobs.MarkNonRetryableAsync(jobId, ct);
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Retrying preview job {JobId} failed; the next sweep tries again", jobId);
        }
    }
}
