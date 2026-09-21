namespace Alexandria.Dto.Files.Streaming.Shuffle;

public sealed record CreateShuffleSessionCommand
{
    public Guid RequestId { get; init; }
    public PlaybackSourceDto Source { get; init; } = null!;
    public Guid? AnchorFileId { get; init; }
    public Guid? AnchorPlaylistItemId { get; init; }
    public Guid? AvoidFirstFileId { get; init; }
    public int Limit { get; init; } = 50;
}