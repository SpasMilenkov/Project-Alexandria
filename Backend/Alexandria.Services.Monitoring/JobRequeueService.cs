using System.Text;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Jobs;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Monitoring;

public class JobRequeueService(
    IUnitOfWork unitOfWork,
    IPreviewService previewService,
    IPublisherService publisher,
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

        return job.Type switch
        {
            JobType.MediaPreview or JobType.DocumentPreview => await RequeuePreviewAsync(job.Id, job.Type, ct),
            JobType.Transpilation => await ResetAndPublishAsync(job.Id, job.Type, TranspilationRoutingKey, ct),
            JobType.LyricsFetch => await RequeueLyricsAsync(job.Id, ct),
            JobType.MetadataEnrichment => await RequeueEnrichmentAsync(job.Id, job.Status, ct),
            _ => JobRequeueOutcome.Unsupported
        };
    }

    public async Task<int> RequeueFailedAsync(JobType type, int limit, CancellationToken ct)
    {
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

    private async Task<JobRequeueOutcome> RequeuePreviewAsync(Guid jobId, JobType type, CancellationToken ct)
    {
        if (!await TryResetAsync(jobId, type, ct))
            return JobRequeueOutcome.NotEligible;

        try
        {
            if (await previewService.RepublishJobAsync(jobId, ct))
                return JobRequeueOutcome.Requeued;
        }
        catch (Exception)
        {
            await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Failed,
                errorDetail: JobRecoveryService.PublishFailedError, ct: ct);
            throw;
        }

        await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Failed,
            errorDetail: JobRecoveryService.SourceGoneError, ct: ct);
        await unitOfWork.Jobs.MarkNonRetryableAsync(jobId, ct);
        return JobRequeueOutcome.SourceUnavailable;
    }

    private async Task<JobRequeueOutcome> RequeueLyricsAsync(Guid jobId, CancellationToken ct)
    {
        var lyrics = await unitOfWork.Lyrics.GetByJobIdAsync(jobId, ct);
        if (lyrics is null)
            return JobRequeueOutcome.SourceUnavailable;

        if (lyrics.SourceProvider == LyricsProvider.Manual)
            return JobRequeueOutcome.NotEligible;

        if (!await TryResetAsync(jobId, JobType.LyricsFetch, ct))
            return JobRequeueOutcome.NotEligible;

        // The tracked row is saved without Update() so its included Job is not written back
        // over the reset that just happened.
        lyrics.Status = LyricsStatus.PendingFetch;
        lyrics.FetchedAt = null;
        await unitOfWork.SaveChangesAsync(ct);

        return await PublishOrFailAsync(jobId, LyricsRoutingKey, ct);
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

        await publisher.PublishAsync(Encoding.UTF8.GetBytes(attempt.FileId.ToString()), EnrichRoutingKey);

        // Retire the old attempt so it leaves the failed list and no longer counts toward the
        // automatic retry cap; the new trigger creates its own job.
        await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Cancelled, errorDetail: SupersededError, ct: ct);
        return JobRequeueOutcome.Requeued;
    }

    private async Task<JobRequeueOutcome> ResetAndPublishAsync(
        Guid jobId, JobType type, string routingKey, CancellationToken ct)
    {
        if (!await TryResetAsync(jobId, type, ct))
            return JobRequeueOutcome.NotEligible;

        return await PublishOrFailAsync(jobId, routingKey, ct);
    }

    private async Task<JobRequeueOutcome> PublishOrFailAsync(Guid jobId, string routingKey, CancellationToken ct)
    {
        try
        {
            await publisher.PublishAsync(Encoding.UTF8.GetBytes(jobId.ToString()), routingKey);
        }
        catch (Exception)
        {
            await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Failed,
                errorDetail: JobRecoveryService.PublishFailedError, ct: ct);
            throw;
        }

        return JobRequeueOutcome.Requeued;
    }

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
