using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Gallery;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Common.Repositories;

/// <summary>
/// Repository for the photo timeline: live files that have a gallery date. The generic reads see
/// only those files, so a lookup here cannot return a document or a trashed photo.
/// </summary>
public interface IGalleryRepository : IRepository<File>
{
    /// <summary>
    /// One keyset page, newest first. Fetches one row past the limit to learn whether another
    /// page exists, instead of counting the whole range.
    /// </summary>
    Task<GalleryPage> GetPageAsync(GalleryPageQuery query, CancellationToken ct = default);

    /// <summary>
    /// Photo counts per calendar month, newest first. Capture dates are already wall-clock
    /// values; upload dates are true instants, so they are bucketed in
    /// <paramref name="timeZoneId"/> (IANA) to land in the viewer's month.
    /// </summary>
    Task<IReadOnlyList<GalleryMonthBucketRow>> GetMonthBucketsAsync(
        Guid ownerId,
        GallerySort sort,
        string timeZoneId,
        CancellationToken ct = default);
}