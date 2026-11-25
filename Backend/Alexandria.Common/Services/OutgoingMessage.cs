namespace Alexandria.Common.Services;

public sealed record OutgoingMessage
{
    public required string RoutingKey { get; init; }
    public required byte[] Body { get; init; }

    /// <summary>Lets a consumer recognise a redelivered duplicate; outbox messages send their row id.</summary>
    public string? MessageId { get; init; }

    public IReadOnlyDictionary<string, object?>? Headers { get; init; }
}
