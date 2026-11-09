using Alexandria.Common.Exceptions;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Gallery;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Gallery;

public sealed partial class GalleryService(IGalleryRepository gallery, ILogger<GalleryService> logger)
    : IGalleryService
{
    public async Task<GalleryPageResponse> GetPageAsync(
        Guid userId,
        GalleryPageFilter filter,
        CancellationToken ct = default)
    {
        GalleryCursor? cursor = null;

        if (filter.Cursor is not null && !GalleryCursorCodec.TryDecode(filter.Cursor, filter.Sort, out cursor))
        {
            LogInvalidCursor(logger, userId, filter.Sort);
            throw new InvalidGalleryCursorException();
        }

        var page = await gallery.GetPageAsync(new GalleryPageQuery
        {
            OwnerId = userId,
            Sort = filter.Sort,
            Cursor = cursor,
            Before = filter.Before,
            Since = filter.Since,
            Limit = filter.Limit,
        }, ct);

        string? nextCursor = null;
        if (page.Next is not null)
            nextCursor = GalleryCursorCodec.Encode(page.Next, filter.Sort);

        return new GalleryPageResponse
        {
            Items = page.Items.Select(GalleryPhotoDto.FromRow).ToList(),
            NextCursor = nextCursor,
        };
    }

    public async Task<GalleryTimelineResponse> GetTimelineAsync(
        Guid userId,
        GallerySort sort,
        string timeZoneId,
        CancellationToken ct = default)
    {
        var buckets = await gallery.GetMonthBucketsAsync(userId, sort, timeZoneId, ct);

        return new GalleryTimelineResponse
        {
            Months = buckets.Select(GalleryMonthDto.FromRow).ToList(),
            Total = buckets.Sum(b => b.Count),
        };
    }
}