namespace Alexandria.Dto.Files.Streaming;

public sealed class StreamHistoryQuery
{
    public Guid? UserId { get; init; }
    public Guid? FileId { get; init; }
    public bool? Completed { get; init; }
    public DateTime? AccessedAfter { get; init; }
    public DateTime? AccessedBefore { get; init; }
    public int CurrentPage { get; init; } = 0;
    public int PageSize { get; init; } = 25;
}