using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Common.Services;

public interface IStreamingRepresentationService
{
    /// <summary>
    /// Creates a single streaming representation record for a given job.
    /// Throws <see cref="TranspilationJobNotFoundException"/> when the job does not exist.
    /// </summary>
    Task<StreamingRepresentationResponse> CreateRepresentationAsync(
        CreateStreamingRepresentationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Bulk-creates one representation per lane produced by the transpiler.
    /// All requests must carry the same <see cref="CreateStreamingRepresentationRequest.JobId"/>.
    /// Throws <see cref="TranspilationJobNotFoundException"/> when the job does not exist.
    /// </summary>
    Task<IEnumerable<StreamingRepresentationResponse>> CreateRepresentationsAsync(
        List<CreateStreamingRepresentationRequest> requests,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a representation by its primary key.
    /// Throws <see cref="StreamingRepresentationNotFoundException"/> when not found.
    /// </summary>
    Task<StreamingRepresentationResponse> GetByIdAsync(
        Guid id,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all representations belonging to a job.
    /// </summary>
    Task<IEnumerable<StreamingRepresentationResponse>> GetByJobIdAsync(
        Guid jobId,
        CancellationToken ct = default);

    /// <summary>
    /// Filtered, paginated query over representations.
    /// </summary>
    Task<PaginatedResult<StreamingRepresentationResponse>> FindRepresentationsAsync(
        StreamingRepresentationQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Transitions a single representation to Ready.
    /// Throws <see cref="InvalidRepresentationStateException"/> when already terminal.
    /// </summary>
    Task MarkReadyAsync(Guid representationId, CancellationToken ct = default);

    /// <summary>
    /// Transitions a single representation to Failed.
    /// Throws <see cref="InvalidRepresentationStateException"/> when already terminal.
    /// </summary>
    Task MarkFailedAsync(Guid representationId, CancellationToken ct = default);

    /// <summary>
    /// Transitions a single representation to Processing.
    /// Throws <see cref="InvalidRepresentationStateException"/> when already terminal.
    /// </summary>
    Task MarkProcessingAsync(Guid representationId, CancellationToken ct = default);

    /// <summary>
    /// Bulk-transitions a set of representations to Ready. No state guard:
    /// intended for use by the transpilation worker after a successful transcode.
    /// </summary>
    Task MarkAllReadyAsync(List<Guid> representationIds, CancellationToken ct = default);

    /// <summary>
    /// Bulk-transitions a set of representations to Failed. No state guard:
    /// intended for cleanup paths inside the transpilation worker.
    /// </summary>
    Task MarkAllFailedAsync(List<Guid> representationIds, CancellationToken ct = default);
}