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
    private static readonly IReadOnlyDictionary<TranspilationStatus, IReadOnlySet<TranspilationStatus>>
        AllowedTransitions =
            new Dictionary<TranspilationStatus, IReadOnlySet<TranspilationStatus>>
            {
                [TranspilationStatus.Queued] = new HashSet<TranspilationStatus>
                    { TranspilationStatus.Processing, TranspilationStatus.Cancelled },
                [TranspilationStatus.Processing] = new HashSet<TranspilationStatus>
                {
                    TranspilationStatus.Ready, TranspilationStatus.Failed, TranspilationStatus.CancellationRequested
                },
                [TranspilationStatus.CancellationRequested] = new HashSet<TranspilationStatus>
                    { TranspilationStatus.Cancelled },
                [TranspilationStatus.Ready] = new HashSet<TranspilationStatus> { TranspilationStatus.Queued },
                [TranspilationStatus.Failed] = new HashSet<TranspilationStatus> { TranspilationStatus.Queued },
                [TranspilationStatus.Cancelled] = new HashSet<TranspilationStatus> { TranspilationStatus.Queued },
                [TranspilationStatus.Partial] = new HashSet<TranspilationStatus> { TranspilationStatus.Queued },
            };


    /// <inheritdoc/>
    public async Task<TranspilationJobDto> CreateJobAsync(
        Guid versionId,
        Guid userId,
        AudioRung[] audioRungs,
        VideoRung[] videoRungs,
        CancellationToken ct = default)
    {
        var isVideo =
            await unitOfWork.ContentObjects.IsVideo(versionId, userId, ct);

        var activeJob = await unitOfWork.TranspilationJobs
            .GetActiveJobForVersionAsync(versionId, ct);

        if (activeJob is not null)
            throw new TranspilationJobConflictException(versionId, activeJob.Id);

        var entity = new TranspilationJob()
        {
            Id = Guid.NewGuid(),
            VersionId = versionId,
            IsVideo = isVideo,
            Status = TranspilationStatus.Queued,
            ProgressPercent = 0,
            AudioRungs = audioRungs.Length == 0 && !isVideo
                ? [AudioRung.Kbps96, AudioRung.Kbps128, AudioRung.Kbps192]
                : audioRungs,
            VideoRungs = videoRungs.Length == 0 && isVideo ? [VideoRung.P360, VideoRung.P1080] : videoRungs,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow,
            UserId = userId
        };
        var created = await unitOfWork.TranspilationJobs.AddAsync(entity, ct);
        await publisherService.PublishAsync(Encoding.UTF8.GetBytes(entity.Id.ToString()), "transpilation.job");
        LogJobCreated(logger, created.Id, versionId);

        return created.ToDto();
    }

    /// <inheritdoc/>
    public async Task<TranspilationJobDto> GetByIdAsync(
        Guid jobId,
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

    public async Task UpdateStatusAsync(
        Guid jobId,
        TranspilationStatus status,
        int? progress = null,
        string? errorDetail = null,
        string? segmentPrefix = null,
        AudioRung[]? audioRungs = null,
        VideoRung[]? videoRungs = null,
        CancellationToken ct = default)
    {
        var exists = await unitOfWork.TranspilationJobs.ExistsAsync(j => j.Id == jobId, ct);

        if (!exists)
            throw new TranspilationJobNotFoundException(jobId);

        await unitOfWork.TranspilationJobs.UpdateStatusAsync(jobId, status, progress, errorDetail, segmentPrefix,
            audioRungs, videoRungs, ct: ct);

        LogJobStatusUpdated(logger, jobId, status);
    }


    /// <inheritdoc/>
    // Called by users to drive state machine transitions.
    public async Task UpdateStatusAsync(
        Guid jobId,
        Guid userId,
        TranspilationStatus targetStatus,
        AudioRung[]? audioRungs = null,
        VideoRung[]? videoRungs = null,
        CancellationToken ct = default)
    {
        var job = await unitOfWork.TranspilationJobs.FirstOrDefaultAsync(
                      j => j.Id == jobId && j.UserId == userId, ct)
                  ?? throw new TranspilationJobNotFoundException(jobId);

        if (!AllowedTransitions.TryGetValue(job.Status, out var allowed) || !allowed.Contains(targetStatus))
            throw new InvalidTranspilationTransitionException(job.Status, targetStatus, jobId);

        if (targetStatus == TranspilationStatus.Queued)
        {
            if (audioRungs is not null) job.AudioRungs = audioRungs;
            if (videoRungs is not null) job.VideoRungs = videoRungs;

            // Reset progress so the UI doesn't show stale 100% on a requeued job.
            await unitOfWork.TranspilationJobs.UpdateStatusAsync(jobId,
                TranspilationStatus.Queued,
                progress: 0,
                audioRungs: audioRungs,
                videoRungs: videoRungs,
                ct: ct);

            await unitOfWork.TranspilationJobs.ClearErrorAsync(jobId, ct);

            await publisherService.PublishAsync(Encoding.UTF8.GetBytes(jobId.ToString()), "transpilation.job");

            LogJobRequeued(logger, jobId);
            return;
        }

        if (targetStatus == TranspilationStatus.CancellationRequested)
        {
            await unitOfWork.TranspilationJobs.UpdateStatusAsync(jobId, TranspilationStatus.CancellationRequested,
                ct: ct);
            LogJobCancellationRequested(logger, jobId);
            return;
        }

        await unitOfWork.TranspilationJobs.UpdateStatusAsync(jobId, targetStatus, ct: ct);
        LogJobStatusUpdated(logger, jobId, targetStatus);
    }

    public async Task UpdateStatusAsync(Guid jobId, Guid userId, TranspilationStatus status,
        CancellationToken ct = default)
    {
        var exists = await unitOfWork.TranspilationJobs.ExistsAsync(j => j.Id == jobId && j.UserId == userId, ct);

        if (!exists) throw new TranspilationJobNotFoundException(jobId);

        await unitOfWork.TranspilationJobs.UpdateStatusAsync(jobId, status, ct: ct);

        LogJobStatusUpdated(logger, jobId, status);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<TranspilationJobDto>> GetStalledJobsAsync(
        TimeSpan threshold,
        CancellationToken ct = default)
    {
        var jobs = await unitOfWork.TranspilationJobs.GetStalledJobsAsync(threshold, ct);
        var result = jobs.Select(j => j.ToDto()).ToList();

        LogStalledJobsFound(logger, result.Count, threshold);

        return result;
    }

    /// <inheritdoc/>
    public async Task<bool> TryClaimJobAsync(
        Guid jobId,
        CancellationToken ct = default)
    {
        var claimed = await unitOfWork.TranspilationJobs.TryClaimJobAsync(jobId, ct);

        if (claimed)
            LogJobClaimed(logger, jobId);
        else
            LogJobClaimSkipped(logger, jobId);

        return claimed;
    }
}