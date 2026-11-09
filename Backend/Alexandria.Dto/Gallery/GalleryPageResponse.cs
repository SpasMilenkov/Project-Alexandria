namespace Alexandria.Dto.Gallery;

public sealed record GalleryPageResponse
{
    public required IReadOnlyList<GalleryPhotoDto> Items { get; init; }

    /// <summary>Pass back unchanged to continue; null when the requested range is exhausted.</summary>
    public string? NextCursor { get; init; }
}