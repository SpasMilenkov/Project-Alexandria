namespace Alexandria.Dto.Files.Streaming;

public sealed class StreamHistoryResponse
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid FileId { get; init; }
    public long PositionSeconds { get; init; }
    public bool Completed { get; init; }
    public DateTimeOffset LastAccessedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}