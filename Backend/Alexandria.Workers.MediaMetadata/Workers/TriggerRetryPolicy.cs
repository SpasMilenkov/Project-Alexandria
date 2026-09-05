using System.Text;
using RabbitMQ.Client;

namespace Alexandria.Workers.MediaMetadata.Workers;

/// <summary>
/// Retry counting for trigger staging failures. Plain requeue preserves the
/// original headers, so the count only moves when we republish the message
/// ourselves with an incremented <c>x-trigger-attempts</c> header.
/// </summary>
internal static class TriggerRetryPolicy
{
    public const string AttemptsHeader = "x-trigger-attempts";

    /// <summary>
    /// The 1-based number of the delivery that just failed: stored attempts + 1.
    /// </summary>
    public static int NextAttemptNumber(IReadOnlyBasicProperties? properties)
        => ReadAttempts(properties) + 1;

    public static bool ShouldPark(int attemptNumber, int maxAttempts)
        => attemptNumber >= maxAttempts;

    public static int ReadAttempts(IReadOnlyBasicProperties? properties)
    {
        if (properties?.Headers is null)
            return 0;

        if (!properties.Headers.TryGetValue(AttemptsHeader, out var raw) || raw is null)
            return 0;

        if (raw is int i)
            return Math.Max(i, 0);

        if (raw is long l)
            return (int)Math.Max(Math.Min(l, int.MaxValue), 0);

        if (raw is byte[] bytes)
        {
            var text = Encoding.UTF8.GetString(bytes);
            if (int.TryParse(text, out var parsed))
                return Math.Max(parsed, 0);
        }

        if (raw is string s && int.TryParse(s, out var fromString))
            return Math.Max(fromString, 0);

        return 0;
    }

    public static ValueTask PublishWithAttemptsAsync(
        IChannel channel,
        string exchange,
        string routingKey,
        byte[] body,
        int attempts,
        CancellationToken ct)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            Headers = new Dictionary<string, object?> { [AttemptsHeader] = attempts }
        };

        return channel.BasicPublishAsync(exchange, routingKey, false, properties, body, ct);
    }
}