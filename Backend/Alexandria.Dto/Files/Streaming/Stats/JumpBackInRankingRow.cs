namespace Alexandria.Dto.Files.Streaming.Stats;

public sealed record JumpBackInRankingRow
{
    public Guid FileId { get; init; }
    public int QualifiedPlayCount { get; init; }
    public long TotalListenedSeconds { get; init; }
    public DateTime LastPlayedAt { get; init; }
}
