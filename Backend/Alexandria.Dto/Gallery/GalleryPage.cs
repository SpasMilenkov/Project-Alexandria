namespace Alexandria.Dto.Gallery;

public sealed record GalleryPage
{
    public required IReadOnlyList<GalleryPhotoRow> Items { get; init; }

    /// <summary>Null when the requested range has no more photos.</summary>
    public GalleryCursor? Next { get; init; }
}