namespace Alexandria.Dto.Files.Streaming;

public sealed class StreamHistoryQuery
{
    public Guid? UserId { get; init; }
    public Guid? FileId { get; init; }
    public bool? Completed { get; init; }
    public DateTime? LastAccessedAfter { get; init; }
    public DateTime? LastAccessedBefore { get; init; }
    public int CurrentPage { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}