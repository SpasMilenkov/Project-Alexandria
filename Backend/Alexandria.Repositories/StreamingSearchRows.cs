namespace Alexandria.Repositories;

internal enum StreamingSearchSource
{
    Metadata = 0,
    Lyrics = 1,
    FileName = 2
}

// One row per page hit plus the total match count. A page past the end still yields a
// single row with null FileId so the total survives an empty page.
internal sealed class StreamingSearchHitRow
{
    public Guid? FileId { get; set; }
    public int? Source { get; set; }
    public long TotalCount { get; set; }
}

internal sealed class StreamingLyricsSnippetRow
{
    public Guid FileId { get; set; }
    public bool IsCurrentVersion { get; set; }
    public string? PlainLyrics { get; set; }
    public string? SyncedLyrics { get; set; }
}
