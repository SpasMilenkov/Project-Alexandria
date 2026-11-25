namespace Alexandria.Infrastructure.Messaging;

public sealed record DeliveryRetryOptions
{
    /// <summary>The delivery that reaches this count is parked instead of retried.</summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>Pause before a retry, so an outage does not burn every attempt in seconds.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(5);
}
