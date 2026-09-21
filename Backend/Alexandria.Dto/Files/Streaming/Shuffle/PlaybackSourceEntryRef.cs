namespace Alexandria.Dto.Files.Streaming.Shuffle;

public sealed record PlaybackSourceEntryRef
{
    public Guid FileId { get; init; }
    public Guid? PlaylistItemId { get; init; }
    public Guid? TranspilationJobId { get; init; }
}