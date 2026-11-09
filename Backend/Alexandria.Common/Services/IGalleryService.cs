using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Gallery;

namespace Alexandria.Common.Services;

/// <summary>Reads the photo timeline of the requesting user.</summary>
public interface IGalleryService
{
    /// <summary>
    /// One page of the user's photos, newest first, continuing from an opaque cursor or starting
    /// at a date.
    /// </summary>
    /// <exception cref="Exceptions.InvalidGalleryCursorException">The cursor cannot be decoded for the sort.</exception>
    Task<GalleryPageResponse> GetPageAsync(Guid userId, GalleryPageFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Photo counts per month for the user's whole library. <paramref name="timeZoneId"/> is an
    /// IANA zone and only affects <see cref="GallerySort.Uploaded"/>.
    /// </summary>
    Task<GalleryTimelineResponse> GetTimelineAsync(
        Guid userId,
        GallerySort sort,
        string timeZoneId,
        CancellationToken ct = default);
}