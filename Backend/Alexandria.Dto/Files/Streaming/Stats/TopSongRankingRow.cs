namespace Alexandria.Dto.Files.Streaming.Stats;

public sealed record TopSongRankingRow
{
    public Guid FileId { get; init; }
    public int QualifiedPlayCount { get; init; }
    public long TotalListenedSeconds { get; init; }
}
