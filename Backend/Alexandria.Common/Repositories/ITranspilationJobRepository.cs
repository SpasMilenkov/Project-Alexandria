using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Common.Repositories;

public interface ITranspilationJobRepository : IRepository<TranspilationJob>
{
    Task<TranspilationJob?> GetByVersionId(Guid versionId, CancellationToken ct = default);
    Task<TranspilationJob?> GetByVersionId(Guid versionId, Guid userId, CancellationToken ct = default);

    Task<PaginatedResult<TranspilationJobWithDetailsDto>> GetWithDetailsAsync(TranspilationJobQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the transpilation job for the given content object that has an active status
    /// (<see cref="TranspilationStatus.Queued"/> or <see cref="TranspilationStatus.Processing"/>), if one exists.
    /// Uses a non-tracked, projected query — safe to call in hot paths.
    /// </summary>
    Task<TranspilationJob?> GetActiveJobForVersionAsync(Guid versionId, CancellationToken ct = default);

    /// <summary>
    /// Returns the transpilation job with the given identifier, eagerly loading its representations.
    /// </summary>
    Task<TranspilationJob?> GetWithRepresentationsAsync(Guid jobId, CancellationToken ct = default);

    Task<PaginatedResult<TranspilationJob>> FindJobsAsync(TranspilationJobQuery query, CancellationToken ct = default);

    Task UpdateStatusAsync(
        Guid jobId,
        TranspilationStatus status,
        int? progress = null,
        string? errorDetail = null,
        string? segmentPrefix = null,
        AudioRung[]? audioRungs = null,
        VideoRung[]? videoRungs = null,
        CancellationToken ct = default);

    Task ClearErrorAsync(Guid jobId, CancellationToken ct = default);
    Task<IEnumerable<TranspilationJob>> GetStalledJobsAsync(TimeSpan threshold, CancellationToken ct = default);

    /// <summary>
    /// Atomically transitions the job from <see cref="TranspilationStatus.Queued"/> to
    /// <see cref="TranspilationStatus.Processing"/> and stamps <c>StartedAt</c>.
    /// Returns <see langword="true"/> when this caller won the claim;
    /// <see langword="false"/> when the job was already claimed or does not exist.
    /// </summary>
    Task<bool> TryClaimJobAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>
    /// Returns the status of a transpilation job
    /// </summary>
    /// <param name="jobId">The id of the job</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A transpilation status enum</returns>
    Task<TranspilationStatus> GetTranspilationStatusAsync(Guid jobId, CancellationToken ct = default);
}