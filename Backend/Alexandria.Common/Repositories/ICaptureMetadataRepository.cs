using Alexandria.Data.Models;

namespace Alexandria.Common.Repositories;

public interface ICaptureMetadataRepository
{
    Task<CaptureMetadata?> GetByVersionIdAsync(Guid versionId, CancellationToken ct = default);

    /// <summary>
    /// Inserts the row or replaces the existing row for its version. Atomic, because a preview
    /// job and the backfill can process the same version at once.
    /// </summary>
    Task UpsertAsync(CaptureMetadata metadata, CancellationToken ct = default);

    /// <summary>
    /// Live, unencrypted, promoted image versions with no row from
    /// <paramref name="extractorVersion"/> or newer, most recently created first.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetExtractionCandidatesAsync(
        int extractorVersion,
        int limit,
        IReadOnlyCollection<Guid> excludedVersionIds,
        CancellationToken ct = default);
}
