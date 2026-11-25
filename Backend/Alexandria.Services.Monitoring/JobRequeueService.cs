using System.Text;
using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Jobs;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Monitoring;

public class JobRequeueService(
    IUnitOfWork unitOfWork,
    IPreviewService previewService,
    IOutbox outbox,
    IMessageBrokerStatus brokerStatus,
    IOptions<JobRecoveryOptions> options) : IJobRequeueService
{
    internal const string TranspilationRoutingKey = "transpilation.job";
    internal const string LyricsRoutingKey = "lyrics.job";
    internal const string EnrichRoutingKey = "media-metadata.enrich";
    internal const string SupersededError = "superseded by manual requeue";

    public async Task<JobRequeueOutcome> RequeueAsync(Guid jobId, CancellationToken ct)
    {
        var job = await unitOfWork.Jobs.GetByIdAsync(jobId, ct);
        if (job is null || job.DeletedAt is not null)
            return JobRequeueOutcome.NotFound;

        // A broker that is only down is fine: the outbox holds the message until it is back.
        MessageBrokerUnavailableException.ThrowIfDisabled(brokerStatus);

        return job.Type switch
        {
            JobType.MediaPreview or JobType.DocumentPreview => await RequeuePreviewAsync(job.Id, job.Type, ct),
            JobType.Transpilation => await ResetAndEnqueueAsync(job.Id, job.Type, TranspilationRoutingKey, ct),
            JobType.LyricsFetch => await RequeueLyricsAsync(job.Id, ct),
            JobType.MetadataEnrichment => await RequeueEnrichmentAsync(job.Id, job.Status, ct),
            _ => JobRequeueOutcome.Unsupported
        };
    }

    public async Task<int> RequeueFailedAsync(JobType type, int limit, CancellationToken ct)
    {
        MessageBrokerUnavailableException.ThrowIfDisabled(brokerStatus);

        var failed = await unitOfWork.Jobs.FindJobsAsync(
            new JobQuery { Status = JobStatus.Failed, Type = type, PageSize = Math.Clamp(limit, 1, 500) }, ct);

        var requeued = 0;
        foreach (var job in failed.Items)
        {
            if (await RequeueAsync(job.Id, ct) == JobRequeueOutcome.Requeued)
                requeued++;
        }

        return requeued;
    }

    private Task<JobRequeueOutcome> RequeuePreviewAsync(Guid jobId, JobType type, CancellationToken ct)
        => unitOfWork.InTransactionAsync(async () =>
        {
            if (!await TryResetAsync(jobId, type, ct))
                return JobRequeueOutcome.NotEligible;

            if (await previewService.RepublishJobAsync(jobId, ct))
                return JobRequeueOutcome.Requeued;

            await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Failed,
                errorDetail: JobRecoveryService.SourceGoneError, ct: ct);
            await unitOfWork.Jobs.MarkNonRetryableAsync(jobId, ct);
            return JobRequeueOutcome.SourceUnavailable;
        }, ct);

    private async Task<JobRequeueOutcome> RequeueLyricsAsync(Guid jobId, CancellationToken ct)
    {
        var lyrics = await unitOfWork.Lyrics.GetByJobIdAsync(jobId, ct);
        if (lyrics is null)
            return JobRequeueOutcome.SourceUnavailable;

        if (lyrics.SourceProvider == LyricsProvider.Manual)
            return JobRequeueOutcome.NotEligible;

        return await unitOfWork.InTransactionAsync(async () =>
        {
            if (!await TryResetAsync(jobId, JobType.LyricsFetch, ct))
                return JobRequeueOutcome.NotEligible;

            // The tracked row is saved without Update() so its included Job is not written back
            // over the reset that just happened.
            lyrics.Status = LyricsStatus.PendingFetch;
            lyrics.FetchedAt = null;
            await unitOfWork.SaveChangesAsync(ct);

            await EnqueueJobAsync(jobId, LyricsRoutingKey, ct);
            return JobRequeueOutcome.Requeued;
        }, ct);
    }

    // Enrichment attempts are immutable batch rows: a retry is a fresh trigger for the file,
    // which the media-metadata pipeline turns into a new job.
    private async Task<JobRequeueOutcome> RequeueEnrichmentAsync(Guid jobId, JobStatus status, CancellationToken ct)
    {
        if (status is not (JobStatus.Failed or JobStatus.Cancelled))
            return JobRequeueOutcome.NotEligible;

        var attempt = await unitOfWork.EssentiaBatchFiles.FirstOrDefaultAsync(f => f.JobId == jobId, ct);
        if (attempt is null)
            return JobRequeueOutcome.SourceUnavailable;

        return await unitOfWork.InTransactionAsync(async () =>
        {
            await outbox.EnqueueAsync(EnrichRoutingKey, Encoding.UTF8.GetBytes(attempt.FileId.ToString()), ct);

            // Retire the old attempt so it leaves the failed list and no longer counts toward the
            // automatic retry cap; the new trigger creates its own job.
            await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Cancelled, errorDetail: SupersededError, ct: ct);
            return JobRequeueOutcome.Requeued;
        }, ct);
    }

    private Task<JobRequeueOutcome> ResetAndEnqueueAsync(
        Guid jobId, JobType type, string routingKey, CancellationToken ct)
        => unitOfWork.InTransactionAsync(async () =>
        {
            if (!await TryResetAsync(jobId, type, ct))
                return JobRequeueOutcome.NotEligible;

            await EnqueueJobAsync(jobId, routingKey, ct);
            return JobRequeueOutcome.Requeued;
        }, ct);

    private Task EnqueueJobAsync(Guid jobId, string routingKey, CancellationToken ct)
        => outbox.EnqueueJobAsync(routingKey, jobId, ct);

    private Task<bool> TryResetAsync(Guid jobId, JobType type, CancellationToken ct)
        => unitOfWork.Jobs.TryResetForManualRequeueAsync(jobId, DateTime.UtcNow - StalledThreshold(type), ct);

    internal TimeSpan StalledThreshold(JobType type)
    {
        var config = options.Value;
        var minutes = type switch
        {
            JobType.Transpilation => config.TranspilationStalledMinutes,
            JobType.LyricsFetch => config.LyricsStalledMinutes,
            _ => config.PreviewStalledMinutes
        };

        return TimeSpan.FromMinutes(Math.Max(minutes, 1));
    }
}
