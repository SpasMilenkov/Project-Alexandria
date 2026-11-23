using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Links;

namespace Alexandria.Common.Repositories;

/// <summary>
/// The link index. Scans are keyed by content object id, so <see cref="IRepository{T}.GetByIdAsync"/>
/// takes one. A file counts as a note when it is markdown below its owner's Notes folder; that is
/// checked on every read, so moving a file in or out of Notes needs no index update.
/// </summary>
public interface INoteLinkRepository : IRepository<NoteLinkScan>
{
    /// <summary>The extractor version that read the content, or null when it was never read.</summary>
    Task<int?> GetScannerVersionAsync(Guid contentObjectId, CancellationToken ct = default);

    /// <summary>
    /// Replaces the content's scan and links with <paramref name="scan"/> and saves at once. Returns
    /// false when another writer stored a scan for the same content in between; both read the same
    /// text, so theirs is just as good. On any failure the content keeps its previous scan and
    /// nothing of <paramref name="scan"/> stays tracked.
    /// </summary>
    Task<bool> ReplaceAsync(NoteLinkScan scan, CancellationToken ct = default);

    /// <summary>
    /// Current texts of live, unencrypted notes no extractor at <paramref name="scannerVersion"/>
    /// has read, newest first.
    /// </summary>
    Task<IReadOnlyList<NoteLinkScanCandidate>> GetScanCandidatesAsync(int scannerVersion, long maxBytes, int limit,
        CancellationToken ct = default);

    /// <summary>
    /// The owner's live notes whose current text links to the target, most recently edited first.
    /// A note never lists itself.
    /// </summary>
    Task<IReadOnlyList<NoteBacklinkDto>> GetBacklinksAsync(LinkTargetKind kind, Guid targetId, Guid userId,
        int limit, CancellationToken ct = default);

    /// <summary>
    /// For each target with any, how many of the owner's live notes link to it, counted the same
    /// way <see cref="GetBacklinksAsync"/> lists them. Targets nobody mentions are left out.
    /// </summary>
    Task<IReadOnlyList<BacklinkCount>> CountBacklinksAsync(LinkTargetKind kind,
        IReadOnlyCollection<Guid> targetIds, Guid userId, CancellationToken ct = default);
}
