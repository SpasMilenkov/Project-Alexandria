using Alexandria.Data.Models;
using Alexandria.Dto.Files;

namespace Alexandria.Common.Repositories;

public interface IDocumentMetadataRepository : IRepository<DocumentMetadata>
{
    Task<DocumentMetadata?> GetByVersionIdAsync(Guid versionId, CancellationToken ct = default);

    /// <summary>
    /// Inserts the row or replaces the existing row for its version. Atomic, because a preview
    /// job and the backfill can process the same version at once.
    /// </summary>
    Task UpsertAsync(DocumentMetadata metadata, CancellationToken ct = default);

    /// <summary>
    /// Live, unencrypted, promoted versions of the given types with no row from
    /// <paramref name="extractorVersion"/> or newer, most recently created first.
    /// </summary>
    Task<IReadOnlyList<DocumentMetadataCandidate>> GetExtractionCandidatesAsync(
        IReadOnlyCollection<string> mimeTypes,
        int extractorVersion,
        int limit,
        IReadOnlyCollection<Guid> excludedVersionIds,
        CancellationToken ct = default);
}
