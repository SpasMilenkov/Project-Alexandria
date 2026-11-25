using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Workers.MediaMetadata.Workers;

/// <summary>
/// Bounded retry for the results and backfill consumers, whose failures used to be requeued
/// unconditionally and hot-looped on poison messages. The final failure is recorded as a
/// worker-cycle incident and dropped; the batch timeout sweep and the auto-tag sweeper recover the
/// affected work from the database.
/// </summary>
internal static partial class ConsumerRetry
{
    private static readonly DeliveryRetryOptions Options = new()
    {
        MaxAttempts = 5,
        RetryDelay = TimeSpan.FromSeconds(5)
    };

    internal static Task HandleFailureAsync(
        DeliveryRetry deliveryRetry,
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        Exception ex,
        IServiceProvider serviceProvider,
        ILogger logger,
        CancellationToken ct)
        => deliveryRetry.HandleFailureAsync(
            channel,
            eventArgs,
            Options,
            _ => RecordAsync(serviceProvider, ex, logger, ct),
            ct);

    private static async Task RecordAsync(
        IServiceProvider serviceProvider, Exception ex, ILogger logger, CancellationToken ct)
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
            LogRecordFailed(logger, recordEx);
        }
    }

    [LoggerMessage(22950, LogLevel.Warning,
        "Could not record the incident for a dropped media-metadata message")]
    private static partial void LogRecordFailed(ILogger logger, Exception ex);
}
