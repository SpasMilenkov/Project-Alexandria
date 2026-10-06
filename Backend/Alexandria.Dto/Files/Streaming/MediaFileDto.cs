namespace Alexandria.Dto.Files.Streaming;

public sealed class MediaFileDto
{
    public Guid FileId { get; set; }
    public string FileName { get; set; }
    public string MimeType { get; set; }

    public Guid CurrentVersionId { get; set; }
    public Guid PlaybackVersionId { get; set; }

    public double? Duration { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Title { get; set; }
    public string? Genre { get; set; }
    public string? Year { get; set; }
    public Guid TranspilationJobId { get; set; }
    public Guid? PlaylistItemId { get; set; }
    public bool IsVideo { get; set; }
    public string? SegmentPrefix { get; set; }

    // Watch progress for the requesting user, left-joined from StreamHistory.
    // Null when the user has no history row for the file.
    public long? PositionSeconds { get; set; }
    public bool? HasFinished { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
}