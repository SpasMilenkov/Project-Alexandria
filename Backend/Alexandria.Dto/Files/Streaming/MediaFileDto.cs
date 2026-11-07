namespace Alexandria.Dto.Files.Streaming;

public sealed class MediaFileDto
{
    public Guid FileId { get; set; }
    public string FileName { get; set; }
    public string MimeType { get; set; }

    public Guid CurrentVersionId { get; set; }
    public Guid PlaybackVersionId { get; set; }

    // Set when the current version has a manual cover. Thumbnail URLs are cached long-term,
    // so clients add this to them to pick up a replaced cover.
    public DateTime? CoverUpdatedAt { get; set; }

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

    // Set only on search results that matched through lyrics rather than metadata or
    // file name: the lyric line containing the match, so the hit is explainable.
    public string? LyricsSnippet { get; set; }
}