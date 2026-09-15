using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Dto.Files.Streaming.Playlist;

namespace Alexandria.Common.Repositories;

public interface ITranspilationJobRepository : IRepository<TranspilationJob>
{
    /// <summary>
    /// Returns the transpilation job for the given generic Job id, via the indexed
    /// TranspilationJob.JobId FK. This is the lookup workers/handlers use, since the
    /// Job id is the canonical identifier flowing through the queue.
    /// </summary>
    Task<TranspilationJob?> GetByJobIdAsync(Guid jobId, CancellationToken ct = default);

    Task<TranspilationJob?> GetByVersionId(Guid versionId, CancellationToken ct = default);
    Task<TranspilationJob?> GetByVersionId(Guid versionId, Guid userId, CancellationToken ct = default);

    Task<PaginatedResult<TranspilationJobWithDetailsDto>> GetWithDetailsAsync(TranspilationJobQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the transpilation job for the given content object that has an active status
    /// (<see cref="JobStatus.Queued"/> or <see cref="JobStatus.Processing"/>), if one exists.
    /// Uses a non-tracked, projected query — safe to call in hot paths.
    /// </summary>
    Task<TranspilationJob?> GetActiveJobForVersionAsync(Guid versionId, CancellationToken ct = default);

    /// <summary>
    /// Returns the transpilation job with the given identifier, eagerly loading its representations.
    /// </summary>
    Task<TranspilationJob?> GetWithRepresentationsAsync(Guid jobId, CancellationToken ct = default);

    Task<PaginatedResult<TranspilationJob>> FindJobsAsync(TranspilationJobQuery query, CancellationToken ct = default);

    /// <summary>
    /// Updates transpilation-specific fields only (rungs, segment prefix). Status, progress,
    /// retry count, and error detail live on the related Job row — see IJobRepository.
    /// </summary>
    Task UpdateDetailsAsync(
        Guid transpilationJobId,
        string? segmentPrefix = null,
        AudioRung[]? audioRungs = null,
        VideoRung[]? videoRungs = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns every job that was created inside the window or completed
    /// inside it (either boundary may match) — the raw material for volume and
    /// rate bucketing. No tracking.
    /// </summary>
    Task<IReadOnlyList<TranspilationJob>> GetJobsTouchingWindowAsync(
        DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// Owner-wide viable transcodes for playlist binding: live Ready jobs with a Ready
    /// representation, on live versions of live files owned by <paramref name="ownerId"/>.
    /// Same viability definition as the streaming grid, projected flat so the
    /// version-selection rule runs in memory. No tracking, unbounded.
    /// </summary>
    Task<IReadOnlyList<ResolvableTranscodeRow>> GetResolvableJobsAsync(
        Guid ownerId, CancellationToken ct = default);
}