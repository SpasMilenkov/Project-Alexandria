namespace Alexandria.Dto.Files.Streaming.Shuffle;

public sealed record ShuffleListenRow
{
    public Guid FileId { get; init; }
    public long ListenedSeconds { get; init; }
    public DateTime EndedAtUtc { get; init; }
}