namespace Alexandria.Dto.Gallery;

public sealed record GalleryPhotoDto
{
    public required Guid FileId { get; init; }
    public required Guid VersionId { get; init; }
    public required string Name { get; init; }
    public required string MimeType { get; init; }

    /// <summary>The folder holding the file; null when it sits at the root.</summary>
    public Guid? DirectoryId { get; init; }

    /// <summary>
    /// The camera's wall clock, labeled UTC. Read it with UTC getters to get the time the
    /// photographer saw. Equals the upload time when the photo carries no capture date.
    /// </summary>
    public required DateTime TakenAt { get; init; }

    /// <summary>A true instant; convert it to the viewer's zone for display.</summary>
    public required DateTime UploadedAt { get; init; }

    public int? Width { get; init; }
    public int? Height { get; init; }

    public static GalleryPhotoDto FromRow(GalleryPhotoRow row) => new()
    {
        FileId = row.FileId,
        VersionId = row.VersionId,
        Name = row.Name,
        MimeType = row.MimeType,
        DirectoryId = row.DirectoryId,
        TakenAt = row.TakenAt,
        UploadedAt = row.UploadedAt,
        Width = row.Width,
        Height = row.Height,
    };
}