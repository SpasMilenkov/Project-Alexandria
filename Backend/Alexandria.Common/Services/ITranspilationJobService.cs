using Alexandria.Common.Exceptions;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Common.Services;

public interface ITranspilationJobService
{
    /// <summary>
    /// Creates a new transpilation job for the specified content object.
    /// </summary>
    /// <param name="versionId">The id of the file version.</param>
    /// <param name="userId">The id the user that owns the file version.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created transpilation job.</returns>
    /// <exception cref="ContentObjectNotFoundException">
    /// Thrown when no content object exists that is linked to the passed version and user Id/>.
    /// </exception>
    /// <exception cref="TranspilationJobConflictException">
    /// Thrown when a job with status <see cref="TranspilationStatus.Queued"/> or
    /// <see cref="TranspilationStatus.Processing"/> already exists for the content object.
    /// </exception>
    Task<TranspilationJobResponse> CreateJobAsync(
        Guid versionId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the transpilation job with the given identifier, including all of its representations.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The job with its representations populated.</returns>
    /// <exception cref="TranspilationJobNotFoundException">
    /// Thrown when no job with <paramref name="jobId"/> exists.
    /// </exception>
    Task<TranspilationJobResponse> GetByIdAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent transpilation job associated with the given content object.
    /// Representations are not eagerly loaded on this path; use
    /// <see cref="GetByIdAsync"/> if you need them.
    /// </summary>
    /// <param name="contentObjectId">The content object identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The associated transpilation job.</returns>
    /// <exception cref="TranspilationJobNotFoundException">
    /// Thrown when no job exists for <paramref name="contentObjectId"/>.
    /// </exception>
    Task<TranspilationJobResponse> GetByContentObjectIdAsync(
        Guid contentObjectId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a paginated list of transpilation jobs matching the given query filters.
    /// Each result includes its associated representations.
    /// </summary>
    /// <param name="query">Filtering and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PaginatedResult<TranspilationJobResponse>> FindJobsAsync(
        TranspilationJobQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the status of a transpilation job, optionally recording progress
    /// and error details.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="status">The new status to apply.</param>
    /// <param name="progress">
    /// Optional progress percentage (0–100). When <see langword="null"/>, the
    /// existing value is preserved.
    /// </param>
    /// <param name="errorDetail">
    /// Optional error detail message. When <see langword="null"/>, the existing
    /// value is preserved.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="TranspilationJobNotFoundException">
    /// Thrown when no job with <paramref name="jobId"/> exists.
    /// </exception>
    Task UpdateStatusAsync(
        Guid jobId,
        TranspilationStatus status,
        int? progress = null,
        string? errorDetail = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all transpilation jobs that have been in the
    /// <see cref="TranspilationStatus.Processing"/> state longer than
    /// <paramref name="threshold"/>, indicating they are stalled.
    /// Intended for consumption by background recovery workers.
    /// </summary>
    /// <param name="threshold">
    /// The maximum acceptable duration for a job to remain in the processing state.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<IEnumerable<TranspilationJobResponse>> GetStalledJobsAsync(
        TimeSpan threshold,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically transitions the job from <see cref="TranspilationStatus.Queued"/> to
    /// <see cref="TranspilationStatus.Processing"/> and stamps <c>StartedAt</c>.
    /// Returns <see langword="false"/> if the job was already claimed by another worker
    /// or does not exist — both are non-error outcomes for the caller.
    /// </summary>
    /// <param name="jobId">The job to claim.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryClaimJobAsync(Guid jobId, CancellationToken ct = default);
}