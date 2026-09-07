using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Common.Repositories;

public interface IStreamingRepresentationRepository : IRepository<StreamingRepresentation>
{
    /// <summary>
    /// Returns all representations belonging to the given job, untracked.
    /// </summary>
    Task<IEnumerable<StreamingRepresentation>> GetByJobIdAsync(
        Guid jobId,
        CancellationToken ct = default);

    /// <summary>
    /// Filtered, paginated query over representations.
    /// </summary>
    Task<PaginatedResult<StreamingRepresentation>> FindRepresentationsAsync(
        StreamingRepresentationQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Transitions a single representation to <see cref="RepresentationStatus.Processing"/>.
    /// </summary>
    Task MarkProcessingAsync(Guid representationId, CancellationToken ct = default);

    /// <summary>
    /// Transitions a single representation to <see cref="RepresentationStatus.Ready"/>
    /// and stamps <see cref="StreamingRepresentation.CompletedAt"/>.
    /// </summary>
    Task MarkReadyAsync(Guid representationId, CancellationToken ct = default);

    /// <summary>
    /// Transitions a single representation to <see cref="RepresentationStatus.Failed"/>
    /// and stamps <see cref="StreamingRepresentation.CompletedAt"/>.
    /// </summary>
    Task MarkFailedAsync(Guid representationId, CancellationToken ct = default);

    /// <summary>
    /// Bulk-transitions a set of representations to <see cref="RepresentationStatus.Processing"/>.
    /// </summary>
    Task MarkAllProcessingAsync(List<Guid> representationIds, CancellationToken ct = default);

    /// <summary>
    /// Bulk-transitions a set of representations to <see cref="RepresentationStatus.Ready"/>
    /// and stamps <see cref="StreamingRepresentation.CompletedAt"/>.
    /// </summary>
    Task MarkAllReadyAsync(List<Guid> representationIds, CancellationToken ct = default);

    /// <summary>
    /// Bulk-transitions a set of representations to <see cref="RepresentationStatus.Failed"/>
    /// and stamps <see cref="StreamingRepresentation.CompletedAt"/>.
    /// </summary>
    Task MarkAllFailedAsync(List<Guid> representationIds, CancellationToken ct = default);

    Task DeleteByTranspilation(Guid transpilationId, CancellationToken ct = default);

    /// <summary>
    /// Per-user transcoded storage: representations of non-deleted transpilation jobs
    /// owned by the user. No tracking.
    /// </summary>
    Task<(long TotalSize, int Count)> GetStorageByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Transcoded bytes per transpilation job owner (same rule as GetStorageByUserAsync).
    /// No tracking.
    /// </summary>
    Task<Dictionary<Guid, long>> GetSizeByOwnerAsync(CancellationToken ct = default);

    /// <summary>
    /// Oldest-first page of representations with no recorded size, limited to ready
    /// rows of ready jobs with a segment prefix. Job included for prefix access.
    /// No tracking.
    /// </summary>
    Task<IReadOnlyList<StreamingRepresentation>> GetMissingSizesAsync(
        int take, CancellationToken ct = default);

    /// <summary>
    /// Sets size only while none is recorded. Returns false when another writer won the race.
    /// </summary>
    Task<bool> TryBackfillSizeAsync(
        Guid representationId, long size, CancellationToken ct = default);
}