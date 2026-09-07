using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Dto.Previews;
using Alexandria.Dto.PreviewsStats;

namespace Alexandria.Common.Repositories;

public interface IPreviewRepository : IRepository<Preview>
{
    public Task<Preview> CreateAsync(Preview file, CancellationToken ct = default);
    public Task<Preview> UpdateAsync(Preview file, CancellationToken ct = default);

    /// <summary>Current all-time artifact count and size total per kind.</summary>
    Task<IReadOnlyList<PreviewKindTotals>> GetKindTotalsAsync(CancellationToken ct = default);

    /// <summary>Artifacts created inside the window, no tracking — volume bucketing input.</summary>
    Task<IReadOnlyList<Preview>> GetCreatedBetweenAsync(
        DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// Per-user preview storage over live files only: previews whose version and file are
    /// not deleted and whose file is owned by the user. No tracking.
    /// </summary>
    Task<(long TotalSize, int Count, IReadOnlyList<PreviewKindTotals> ByKind)> GetStorageByUserAsync(
        Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Live preview bytes per file owner (same live-only rule as GetStorageByUserAsync).
    /// No tracking.
    /// </summary>
    Task<Dictionary<Guid, long>> GetSizeByOwnerAsync(CancellationToken ct = default);

    /// <summary>
    /// Single preview with its version and file for ownership checks. No tracking.
    /// </summary>
    Task<Preview?> GetWithFileAsync(Guid previewId, CancellationToken ct = default);

    /// <summary>
    /// Live previews of one file, optionally only those created before a cutoff
    /// (period cleanup). No tracking, version included for key fallback.
    /// </summary>
    Task<IReadOnlyList<Preview>> GetByFileAsync(
        Guid fileId, DateTime? createdBefore, CancellationToken ct = default);

    /// <summary>
    /// A user's preview artifacts over live files, optionally scoped to one file and/or
    /// created before a cutoff. Newest first with an id tiebreak for stable paging.
    /// </summary>
    Task<PaginatedResult<UserPreviewDto>> GetListByUserAsync(
        Guid userId, Guid? fileId, DateTime? createdBefore, int page, int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Oldest-first page of previews with no recorded size, for the startup backfill.
    /// Tracked, version included for object-key fallback.
    /// </summary>
    Task<IReadOnlyList<Preview>> GetMissingSizesAsync(
        int take, CancellationToken ct = default);

    /// <summary>
    /// Sets size only while none is recorded. Returns false when another writer won the race.
    /// </summary>
    Task<bool> TryBackfillSizeAsync(
        Guid previewId, long size, CancellationToken ct = default);
}