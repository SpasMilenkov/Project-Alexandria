namespace Alexandria.Dto.Files.Streaming.Shuffle;

public sealed record PlaybackSourceDto
{
    public bool IsVideo { get; init; }
    public Guid? PlaylistId { get; init; }
}