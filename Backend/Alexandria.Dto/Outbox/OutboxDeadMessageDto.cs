namespace Alexandria.Dto.Outbox;

public sealed record OutboxDeadMessageDto
{
    public required Guid Id { get; init; }
    public required string RoutingKey { get; init; }
    public required int Attempts { get; init; }
    public string? LastError { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime DeadAt { get; init; }
}
