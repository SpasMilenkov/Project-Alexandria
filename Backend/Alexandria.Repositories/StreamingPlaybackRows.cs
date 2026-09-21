namespace Alexandria.Repositories;

internal sealed class StreamingFileRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public Guid CurrentVersionId { get; set; }
    public double? Duration { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Title { get; set; }
    public string? Genre { get; set; }
    public string? Year { get; set; }
    public StreamingJobRow? Job { get; set; }
}

internal sealed class StreamingJobRow
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public bool IsVideo { get; set; }
    public string? SegmentPrefix { get; set; }
}

internal sealed class PlaylistStreamingRow
{
    public Guid ItemId { get; set; }
    public int Position { get; set; }
    public Guid FileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public Guid CurrentVersionId { get; set; }
    public Guid PlaybackVersionId { get; set; }
    public double? Duration { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Title { get; set; }
    public string? Genre { get; set; }
    public string? Year { get; set; }
    public Guid JobId { get; set; }
    public bool IsVideo { get; set; }
    public string? SegmentPrefix { get; set; }
}

internal sealed class LibraryPlaybackRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public Guid CurrentVersionId { get; set; }
    public double? Duration { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Title { get; set; }
    public string? Genre { get; set; }
    public string? Year { get; set; }
    public List<Guid> LiveVersionIds { get; set; } = [];
}

internal sealed record PlaylistAnchorKey(int Position, Guid ItemId);