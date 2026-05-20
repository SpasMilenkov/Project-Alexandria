namespace Alexandria.Dto.Files.Streaming;

public sealed class StartSessionRequest
{
    public Guid FileId { get; init; }
    public long StartPositionSeconds { get; init; }
}