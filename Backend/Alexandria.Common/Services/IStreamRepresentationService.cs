using Alexandria.Common.Exceptions;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Common.Services;

public interface IStreamingRepresentationService
{
    /// <summary>
    /// Creates a new streaming representation attached to an existing transpilation job.
    /// </summary>
    /// <param name="request">The representation creation parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created streaming representation.</returns>
    /// <exception cref="TranspilationJobNotFoundException">
    /// Thrown when no transpilation job exists with the ID specified in <paramref name="request"/>.
    /// </exception>
    Task<StreamingRepresentationResponse> CreateRepresentationAsync(
        CreateStreamingRepresentationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the streaming representation with the given identifier.
    /// </summary>
    /// <param name="id">The representation identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="StreamingRepresentationNotFoundException">
    /// Thrown when no representation with <paramref name="id"/> exists.
    /// </exception>
    Task<StreamingRepresentationResponse> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns all streaming representations associated with the given transpilation job.
    /// Returns an empty collection when no representations exist yet — this is not an error.
    /// </summary>
    /// <param name="jobId">The transpilation job identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IEnumerable<StreamingRepresentationResponse>> GetByJobIdAsync(
        Guid jobId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a paginated list of streaming representations matching the given query filters.
    /// </summary>
    /// <param name="query">Filtering and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PaginatedResult<StreamingRepresentationResponse>> FindRepresentationsAsync(
        StreamingRepresentationQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Marks a representation as <see cref="RepresentationStatus.Ready"/> and
    /// records the storage path prefix where its segments reside.
    /// </summary>
    /// <param name="representationId">The representation identifier.</param>
    /// <param name="segmentPrefix">
    /// The storage path prefix for the completed segments,
    /// e.g. <c>{fileId}/v/1080p_av1</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="StreamingRepresentationNotFoundException">
    /// Thrown when no representation with <paramref name="representationId"/> exists.
    /// </exception>
    /// <exception cref="InvalidRepresentationStateException">
    /// Thrown when the representation is already in a terminal state
    /// (<see cref="RepresentationStatus.Ready"/> or <see cref="RepresentationStatus.Failed"/>).
    /// </exception>
    Task MarkReadyAsync(
        Guid representationId,
        string segmentPrefix,
        CancellationToken ct = default);

    /// <summary>
    /// Marks a representation as <see cref="RepresentationStatus.Failed"/>.
    /// </summary>
    /// <param name="representationId">The representation identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="StreamingRepresentationNotFoundException">
    /// Thrown when no representation with <paramref name="representationId"/> exists.
    /// </exception>
    /// <exception cref="InvalidRepresentationStateException">
    /// Thrown when the representation is already in a terminal state
    /// (<see cref="RepresentationStatus.Ready"/> or <see cref="RepresentationStatus.Failed"/>).
    /// </exception>
    Task MarkFailedAsync(Guid representationId, CancellationToken ct = default);

    /// <summary>
    /// Marks the representation as <see cref="RepresentationStatus.Processing"/>,
    /// indicating that an ffmpeg encode has started.
    /// </summary>
    /// <param name="representationId">The representation identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="StreamingRepresentationNotFoundException">
    /// Thrown when no representation with <paramref name="representationId"/> exists.
    /// </exception>
    /// <exception cref="InvalidRepresentationStateException">
    /// Thrown when the representation is already in a terminal state.
    /// </exception>
    Task MarkProcessingAsync(Guid representationId, CancellationToken ct = default);
}