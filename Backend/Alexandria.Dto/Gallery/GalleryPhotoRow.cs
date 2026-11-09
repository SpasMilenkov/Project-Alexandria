namespace Alexandria.Dto.Gallery;

public sealed record GalleryPhotoRow
{
    public required Guid FileId { get; init; }
    public required Guid VersionId { get; init; }
    public required string Name { get; init; }
    public required string MimeType { get; init; }

    /// <summary>The folder holding the file; null when it sits at the root.</summary>
    public Guid? DirectoryId { get; init; }

    /// <summary>The value the page is ordered by, which also becomes the next cursor.</summary>
    public required DateTime SortDate { get; init; }

    public required DateTime TakenAt { get; init; }
    public required DateTime UploadedAt { get; init; }

    /// <summary>Display dimensions after orientation; null until metadata has been read.</summary>
    public int? Width { get; init; }

    public int? Height { get; init; }
}