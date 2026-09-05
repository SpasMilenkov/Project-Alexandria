using System.Text;
using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Extensions;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public partial class TranspilationJobService(
    IUnitOfWork unitOfWork,
    IPublisherService publisherService,
    ILogger<TranspilationJobService> logger) : ITranspilationJobService
{
    private static readonly IReadOnlyDictionary<JobStatus, IReadOnlySet<JobStatus>>
        AllowedTransitions =
            new Dictionary<JobStatus, IReadOnlySet<JobStatus>>
            {
                [JobStatus.Queued] = new HashSet<JobStatus>
                    { JobStatus.Processing, JobStatus.Cancelled },
                [JobStatus.Processing] = new HashSet<JobStatus>
                {
                    JobStatus.Ready, JobStatus.Failed, JobStatus.CancellationRequested
                },
                [JobStatus.CancellationRequested] = new HashSet<JobStatus>
                    { JobStatus.Cancelled },
                [JobStatus.Ready] = new HashSet<JobStatus> { JobStatus.Queued },
                [JobStatus.Failed] = new HashSet<JobStatus> { JobStatus.Queued },
                [JobStatus.Cancelled] = new HashSet<JobStatus> { JobStatus.Queued },
                [JobStatus.Partial] = new HashSet<JobStatus> { JobStatus.Queued },
            };

    /// <inheritdoc/>
    public async Task<TranspilationJobDto> CreateJobAsync(
        Guid versionId,
        Guid userId,
        AudioRung[] audioRungs,
        VideoRung[] videoRungs,
        CancellationToken ct = default)
    {
        var isVideo = await unitOfWork.ContentObjects.IsVideo(versionId, userId, ct);

        var activeJob = await unitOfWork.TranspilationJobs.GetActiveJobForVersionAsync(versionId, ct);
        if (activeJob is not null)
            throw new TranspilationJobConflictException(versionId, activeJob.Id);

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Queued,
            ProgressPercent = 0,
            RetryCount = 0,
            Type = JobType.Transpilation,
            UserId = userId,
        };

        await unitOfWork.Jobs.AddAsync(job, ct);

        var transpilation = new TranspilationJob
        {
            Id = Guid.NewGuid(),
            VersionId = versionId,
            IsVideo = isVideo,
            JobId = job.Id,
            AudioRungs = audioRungs.Length == 0 && !isVideo
                ? [AudioRung.Kbps96, AudioRung.Kbps128, AudioRung.Kbps192]
                : audioRungs,
            VideoRungs = videoRungs.Length == 0 && isVideo ? [VideoRung.P360, VideoRung.P1080] : videoRungs,
            CreatedAt = DateTime.UtcNow,
            UserId = userId
        };

        await unitOfWork.TranspilationJobs.AddAsync(transpilation, ct);

        // Job.Id is the canonical id published to the queue - it's what the worker
        // claims and reports status against.
        await publisherService.PublishAsync(Encoding.UTF8.GetBytes(job.Id.ToString()), "transpilation.job");
        LogJobCreated(logger, transpilation.Id, versionId);

        transpilation.Job = job; // set so ToDto() below doesn't need a reload
        return transpilation.ToDto();
    }

    /// <inheritdoc/>
    public async Task<TranspilationJobDto> GetByIdAsync(
        Guid jobId, // TranspilationJob.Id
        CancellationToken ct = default)
    {
        var job = await unitOfWork.TranspilationJobs.GetWithRepresentationsAsync(jobId, ct)
                  ?? throw new TranspilationJobNotFoundException(jobId);

        return job.ToDto();
    }

    public async Task<PaginatedResult<TranspilationJobWithDetailsDto>> GetWithDetailsAsync(TranspilationJobQuery query,
        CancellationToken ct = default)
    {
        return await unitOfWork.TranspilationJobs.GetWithDetailsAsync(query, ct);
    }

    /// <inheritdoc/>
    public async Task<TranspilationJobDto> GetByVersionId(
        Guid versionId,
        CancellationToken ct = default)
    {
        var job = await unitOfWork.TranspilationJobs.GetByVersionId(versionId, ct)
                  ?? throw new TranspilationJobNotFoundException(versionId);

        return job.ToDto();
    }

    /// <inheritdoc/>
    public async Task<PaginatedResult<TranspilationJobDto>> FindJobsAsync(TranspilationJobQuery query,
        CancellationToken ct = default)
    {
        var result = await unitOfWork.TranspilationJobs.FindJobsAsync(query, ct);

        return new PaginatedResult<TranspilationJobDto>
        {
            Items = result.Items.Select(j => j.ToDto()).ToList(),
            TotalCount = result.TotalCount,
            CurrentPage = result.CurrentPage,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages
        };
    }

    /// <inheritdoc/>
    // Called by the worker/handler. jobId here is Job.Id.
    public async Task UpdateStatusAsync(
        Guid jobId,
        JobStatus status,
        int? progress = null,
        string? errorDetail = null,
        string? segmentPrefix = null,
        AudioRung[]? audioRungs = null,
        VideoRung[]? videoRungs = null,
        CancellationToken ct = default)
    {
        var transpilation = await unitOfWork.TranspilationJobs.GetByJobIdAsync(jobId, ct)
                            ?? throw new TranspilationJobNotFoundException(jobId);

        await unitOfWork.Jobs.UpdateStatusAsync(jobId, status, progress, errorDetail, ct);

        if (segmentPrefix is not null || audioRungs is not null || videoRungs is not null)
        {
            await unitOfWork.TranspilationJobs.UpdateDetailsAsync(
                transpilation.Id, segmentPrefix, audioRungs, videoRungs, ct);
        }

        LogJobStatusUpdated(logger, jobId, status);
    }

    /// <inheritdoc/>
    // Called by users to drive state machine transitions. jobId here is TranspilationJob.Id.
    public async Task UpdateStatusAsync(
        Guid jobId,
        Guid userId,
        JobStatus targetStatus,
        AudioRung[]? audioRungs = null,
        VideoRung[]? videoRungs = null,
        CancellationToken ct = default)
    {
        var transpilation = await unitOfWork.TranspilationJobs.FirstOrDefaultAsync(
                                j => j.Id == jobId && j.UserId == userId, ct)
                            ?? throw new TranspilationJobNotFoundException(jobId);

        if (!AllowedTransitions.TryGetValue(transpilation.Job.Status, out var allowed) ||
            !allowed.Contains(targetStatus))
            throw new InvalidTranspilationTransitionException(transpilation.Job.Status, targetStatus, jobId);

        if (targetStatus == JobStatus.Queued)
        {
            if (audioRungs is not null) transpilation.AudioRungs = audioRungs;
            if (videoRungs is not null) transpilation.VideoRungs = videoRungs;

            await unitOfWork.TranspilationJobs.UpdateDetailsAsync(
                transpilation.Id, audioRungs: audioRungs, videoRungs: videoRungs, ct: ct);

            // Reset progress so the UI doesn't show stale 100% on a requeued job.
            await unitOfWork.Jobs.UpdateStatusAsync(transpilation.JobId, JobStatus.Queued, progress: 0, ct: ct);
            await unitOfWork.Jobs.ClearErrorAsync(transpilation.JobId, ct);

            await publisherService.PublishAsync(
                Encoding.UTF8.GetBytes(transpilation.JobId.ToString()), "transpilation.job");

            LogJobRequeued(logger, jobId);
            return;
        }

        if (targetStatus == JobStatus.CancellationRequested)
        {
            await unitOfWork.Jobs.UpdateStatusAsync(transpilation.JobId, JobStatus.CancellationRequested, ct: ct);
            LogJobCancellationRequested(logger, jobId);
            return;
        }

        await unitOfWork.Jobs.UpdateStatusAsync(transpilation.JobId, targetStatus, ct: ct);
        LogJobStatusUpdated(logger, jobId, targetStatus);
    }

    /// <inheritdoc/>
    public async Task UpdateStatusAsync(Guid jobId, Guid userId, JobStatus status,
        CancellationToken ct = default)
    {
        var transpilation = await unitOfWork.TranspilationJobs.FirstOrDefaultAsync(
                                j => j.Id == jobId && j.UserId == userId, ct)
                            ?? throw new TranspilationJobNotFoundException(jobId);

        await unitOfWork.Jobs.UpdateStatusAsync(transpilation.JobId, status, ct: ct);

        LogJobStatusUpdated(logger, jobId, status);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<TranspilationJobDto>> GetStalledJobsAsync(
        TimeSpan threshold,
        CancellationToken ct = default)
    {
        var stalledJobs = await unitOfWork.Jobs.GetStalledJobsAsync(threshold, ct);
        var transpilationStalledJobs = stalledJobs.Where(j => j.Type == JobType.Transpilation).ToList();

        var result = new List<TranspilationJobDto>(transpilationStalledJobs.Count);
        foreach (var job in transpilationStalledJobs)
        {
            var transpilation = await unitOfWork.TranspilationJobs.GetByJobIdAsync(job.Id, ct);
            if (transpilation is null) continue; // shouldn't happen, but don't fail the whole sweep over one row

            transpilation.Job = job; // already have it in hand, skip a redundant lookup
            result.Add(transpilation.ToDto());
        }

        LogStalledJobsFound(logger, result.Count, threshold);

        return result;
    }

    /// <inheritdoc/>
    // Called by the worker/handler. jobId here is Job.Id.
    public async Task<bool> TryClaimJobAsync(
        Guid jobId,
        CancellationToken ct = default)
    {
        var claimed = await unitOfWork.Jobs.TryClaimJobAsync(jobId, ct);

        if (claimed)
            LogJobClaimed(logger, jobId);
        else
            LogJobClaimSkipped(logger, jobId);

        return claimed;
    }
}