using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Common.Repositories;

public interface IStreamingRepresentationRepository : IRepository<StreamingRepresentation>
{
    Task<IEnumerable<StreamingRepresentation>> GetByJobIdAsync(Guid jobId, CancellationToken ct = default);

    Task<PaginatedResult<StreamingRepresentation>> FindRepresentationsAsync(StreamingRepresentationQuery query,
        CancellationToken ct = default);

    Task<StreamingRepresentation?> GetByVersionIdAsync(Guid versionId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Transitions the representation to <see cref="RepresentationStatus.Processing"/>.
    /// </summary>
    Task MarkProcessingAsync(Guid representationId, CancellationToken ct = default);

    Task MarkReadyAsync(Guid representationId, string segmentPrefix, CancellationToken ct = default);
    Task MarkFailedAsync(Guid representationId, CancellationToken ct = default);
}