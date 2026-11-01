using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Workers.MediaMetadata.Workers;

/// <summary>
/// Bounded retry for consumers whose failures used to be requeued unconditionally, which made a
/// poison message redeliver in a hot loop. Retries are counted in a header, and the final failure
/// is recorded as a worker-cycle incident and dropped; the batch timeout sweep and the auto-tag
/// sweeper recover the affected work from the database.
/// </summary>
internal static class ConsumerRetry
{
    internal const int MaxAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    /// <returns><see langword="true"/> when the message was given up on rather than retried.</returns>
    internal static async Task<bool> HandleFailureAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        string exchangeName,
        Exception ex,
        IServiceProvider serviceProvider,
        CancellationToken ct)
    {
        var attempt = TriggerRetryPolicy.NextAttemptNumber(eventArgs.BasicProperties);

        if (TriggerRetryPolicy.ShouldPark(attempt, MaxAttempts))
        {
            await RecordAsync(serviceProvider, ex, ct);
            await channel.BasicAckAsync(eventArgs.DeliveryTag, false, ct);
            return true;
        }

        try
        {
            await Task.Delay(RetryDelay, ct);
            await TriggerRetryPolicy.PublishWithAttemptsAsync(
                channel, exchangeName, eventArgs.RoutingKey, eventArgs.Body.ToArray(), attempt, ct);
            await channel.BasicAckAsync(eventArgs.DeliveryTag, false, ct);
        }
        catch (Exception retryEx) when (retryEx is not OperationCanceledException)
        {
            await channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: true, ct);
        }

        return false;
    }

    private static async Task RecordAsync(IServiceProvider serviceProvider, Exception ex, CancellationToken ct)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await WorkerCycleFailureRecorder.RecordAsync(unitOfWork, ServiceType.MediaMetadata, "media-metadata", ex,
                ct);
        }
        catch (Exception recordEx) when (recordEx is not OperationCanceledException)
        {
            // Recording is best effort; dropping the message must not depend on the database.
        }
    }
}
