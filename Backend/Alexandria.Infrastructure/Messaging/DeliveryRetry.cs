using System.Text;
using Alexandria.Common.Services;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Infrastructure.Messaging;

/// <summary>
/// Bounded, counted redelivery for consumers. A plain requeue keeps the original headers, so the
/// count only moves when the message is republished with a raised <c>x-delivery-attempts</c>
/// header. Republishing goes through the confirmed publisher and the original is acked only after
/// the broker confirmed the copy, so a failed delivery is never lost on the way.
/// </summary>
public sealed partial class DeliveryRetry(IPublisherService publisher, ILogger<DeliveryRetry> logger)
{
    public const string AttemptsHeader = "x-delivery-attempts";

    private static readonly DeliveryRetryOptions DefaultOptions = new();

    /// <summary>
    /// Below the attempt limit: waits, republishes the message with the count raised and acks the
    /// original. At the limit: runs <paramref name="parkAsync"/> with the attempt number, then acks.
    /// When the republish or the park fails, the original is requeued instead, after the retry
    /// delay when the park failed.
    /// </summary>
    public async Task HandleFailureAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        DeliveryRetryOptions options,
        Func<int, Task> parkAsync,
        CancellationToken ct)
    {
        var attempt = NextAttemptNumber(delivery.BasicProperties);

        if (attempt >= Math.Max(options.MaxAttempts, 1))
        {
            await ParkAsync(channel, delivery, attempt, parkAsync, options.RetryDelay, ct);
            return;
        }

        try
        {
            if (options.RetryDelay > TimeSpan.Zero)
                await Task.Delay(options.RetryDelay, ct);

            await RepublishAsync(delivery, delivery.RoutingKey, attempt, ct);
            await DeliverySettlement.AckAsync(channel, delivery.DeliveryTag, logger, ct);
        }
        // Only our own cancellation passes: the client reports an RPC timeout as a cancellation
        // too, and that one is a failed republish to log and requeue.
        catch (Exception ex) when (!IsStopping(ex, ct))
        {
            LogRetryFailed(logger, ex, delivery.RoutingKey, attempt);
            await DeliverySettlement.NackAsync(channel, delivery.DeliveryTag, requeue: true, logger, ct);
        }
    }

    /// <summary>Parks a delivery that can never succeed, whatever its count.</summary>
    public Task ParkNowAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        Func<int, Task> parkAsync,
        CancellationToken ct)
        => ParkAsync(channel, delivery, NextAttemptNumber(delivery.BasicProperties), parkAsync,
            DefaultOptions.RetryDelay, ct);

    /// <summary>Sends the delivery's body to <paramref name="routingKey"/> carrying the attempt count.</summary>
    public Task RepublishAsync(BasicDeliverEventArgs delivery, string routingKey, int attempts, CancellationToken ct)
        => publisher.PublishAsync(new OutgoingMessage
        {
            RoutingKey = routingKey,
            Body = delivery.Body.ToArray(),
            MessageId = delivery.BasicProperties.MessageId,
            Headers = new Dictionary<string, object?> { [AttemptsHeader] = attempts }
        }, ct);

    /// <summary>The 1-based number of the delivery that just failed: stored attempts + 1.</summary>
    public static int NextAttemptNumber(IReadOnlyBasicProperties? properties)
        => ReadAttempts(properties) + 1;

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

        if (raw is byte[] bytes && int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed))
            return Math.Max(parsed, 0);

        if (raw is string s && int.TryParse(s, out var fromString))
            return Math.Max(fromString, 0);

        return 0;
    }

    private static bool IsStopping(Exception ex, CancellationToken ct)
        => ex is OperationCanceledException && ct.IsCancellationRequested;

    private async Task ParkAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        int attempt,
        Func<int, Task> parkAsync,
        TimeSpan retryDelay,
        CancellationToken ct)
    {
        try
        {
            await parkAsync(attempt);
            await DeliverySettlement.AckAsync(channel, delivery.DeliveryTag, logger, ct);
        }
        catch (Exception ex) when (!IsStopping(ex, ct))
        {
            // Keep the message rather than drop it while the broker or database is too sick to park.
            LogParkFailed(logger, ex, delivery.RoutingKey, attempt);

            // A requeue keeps the attempt count, so the redelivery parks again at once. Without
            // the pause an outage spins through park attempts as fast as the broker redelivers.
            if (retryDelay > TimeSpan.Zero)
                await Task.Delay(retryDelay, ct);

            await DeliverySettlement.NackAsync(channel, delivery.DeliveryTag, requeue: true, logger, ct);
        }
    }
}
