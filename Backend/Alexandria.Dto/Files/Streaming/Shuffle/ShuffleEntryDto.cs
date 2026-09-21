namespace Alexandria.Dto.Files.Streaming.Shuffle;

public sealed record ShuffleEntryDto
{
    public int Position { get; init; }
    public MediaFileDto File { get; init; } = null!;
}