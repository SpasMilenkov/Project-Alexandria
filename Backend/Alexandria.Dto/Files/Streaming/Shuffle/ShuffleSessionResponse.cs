namespace Alexandria.Dto.Files.Streaming.Shuffle;

public sealed record ShuffleSessionResponse
{
    public Guid SessionId { get; init; }
    public PlaybackSourceDto Source { get; init; } = null!;
    public int AlgorithmVersion { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public int TotalCount { get; init; }
    public int? AnchorPosition { get; init; }
    public int Offset { get; init; }
    public int ScannedCount { get; init; }
    public int? NextOffset { get; init; }
    public IReadOnlyList<ShuffleEntryDto> Items { get; init; } = [];
}