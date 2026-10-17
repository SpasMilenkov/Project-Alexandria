using Alexandria.Data.Models;

namespace Alexandria.Common.Repositories;

public interface IContentObjectRepository : IRepository<ContentObject>
{
    Task<ContentObject?> HashExistsAsync(byte[] hash, CancellationToken ct = default);

    /// <summary>
    /// Batch lookup for the temp sweeper. Returns live rows matching any of the
    /// given hashes. Orphaned rows are included so callers can distinguish a
    /// sole copy from a promoted survivor; soft-deleted rows are excluded.
    /// </summary>
    Task<List<ContentObject>> GetByHashesAsync(IEnumerable<byte[]> hashes, CancellationToken ct = default);

    /// <summary>
    /// Atomically hard-deletes an orphaned row only if it is still orphaned,
    /// past retention, and referenced by no live version. Returns true when a
    /// row was deleted. A concurrent revive either commits first (the claim sees
    /// the new version and skips) or commits after (its version insert fails
    /// loudly on the missing row instead of silently losing bytes).
    /// </summary>
    Task<bool> TryClaimOrphanedDeleteAsync(Guid id, DateTime cutoff, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> MarkOrphaned(DateTime time, CancellationToken ct = default);
    Task<int> ClearOrphaned(DateTime time, CancellationToken ct = default);

    Task<bool> IsVideo(
        Guid versionId,
        Guid userId,
        CancellationToken ct = default);
}