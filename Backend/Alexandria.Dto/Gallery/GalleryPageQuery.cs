using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Gallery;

/// <summary>
/// One page of the photo timeline, newest first. <see cref="Cursor"/> continues a previous
/// page; <see cref="Before"/> starts at a date instead, which is how the scrubber jumps.
/// <see cref="Since"/> stops the page at a lower bound, so a month section can load only itself.
/// For <see cref="GallerySort.Taken"/> the bounds are wall-clock values labeled UTC, matching
/// how capture times are stored.
/// </summary>
public sealed record GalleryPageQuery
{
    public required Guid OwnerId { get; init; }
    public required GallerySort Sort { get; init; }
    public GalleryCursor? Cursor { get; init; }
    public DateTime? Before { get; init; }
    public DateTime? Since { get; init; }
    public required int Limit { get; init; }
}