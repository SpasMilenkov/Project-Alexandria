using Alexandria.Common;
using Alexandria.Common.Exceptions;
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
    ILogger<TranspilationJobService> logger) : ITranspilationJobService
{
    /// <inheritdoc/>
    public async Task<TranspilationJobResponse> CreateJobAsync(
        Guid versionId,
        Guid userId,
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
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow,
            UserId = userId
        };
        var created = await unitOfWork.TranspilationJobs.AddAsync(entity, ct);

        LogJobCreated(logger, created.Id, versionId);

        return created.ToResponse();
    }

    /// <inheritdoc/>
    public async Task<TranspilationJobResponse> GetByIdAsync(
        Guid jobId,
        CancellationToken ct = default)
    {
        var job = await unitOfWork.TranspilationJobs.GetWithRepresentationsAsync(jobId, ct)
                  ?? throw new TranspilationJobNotFoundException(jobId);

        return job.ToResponse();
    }

    /// <inheritdoc/>
    public async Task<TranspilationJobResponse> GetByVersionId(
        Guid versionId,
        CancellationToken ct = default)
    {
        var job = await unitOfWork.TranspilationJobs.GetByVersionId(versionId, ct)
                  ?? throw new TranspilationJobNotFoundException(versionId);

        return job.ToResponse();
    }

    /// <inheritdoc/>
    public async Task<PaginatedResult<TranspilationJobResponse>> FindJobsAsync(TranspilationJobQuery query,
        CancellationToken ct = default)
    {
        var result = await unitOfWork.TranspilationJobs.FindJobsAsync(query, ct);

        return new PaginatedResult<TranspilationJobResponse>
        {
            Items = result.Items.Select(j => j.ToResponse()).ToList(),
            TotalCount = result.TotalCount,
            CurrentPage = result.CurrentPage,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages
        };
    }

    /// <inheritdoc/>
    public async Task UpdateStatusAsync(
        Guid jobId,
        TranspilationStatus status,
        int? progress = null,
        string? errorDetail = null,
        string? segmentPrefix = null,
        CancellationToken ct = default)
    {
        var exists = await unitOfWork.TranspilationJobs.ExistsAsync(j => j.Id == jobId, ct);

        if (!exists)
            throw new TranspilationJobNotFoundException(jobId);

        await unitOfWork.TranspilationJobs.UpdateStatusAsync(jobId, status, progress, errorDetail, segmentPrefix, ct);

        LogJobStatusUpdated(logger, jobId, status);
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
    public async Task<IEnumerable<TranspilationJobResponse>> GetStalledJobsAsync(
        TimeSpan threshold,
        CancellationToken ct = default)
    {
        var jobs = await unitOfWork.TranspilationJobs.GetStalledJobsAsync(threshold, ct);
        var result = jobs.Select(j => j.ToResponse()).ToList();

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