using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Alexandria.Infrastructure.Messaging;

/// <summary>
/// Shared startup for queue consumers: waits for the broker instead of crashing the host,
/// and retries the topology/consume setup when it fails. Once a consumer is registered,
/// the client's topology recovery re-attaches it after a broker restart.
/// </summary>
public static partial class ConsumerStartup
{
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(60);

    public static async Task RunAsync(
        MessageBrokerConnection broker,
        Func<IConnection, CancellationToken, Task> setup,
        ILogger logger,
        TimeSpan? retryDelay = null,
        CancellationToken ct = default)
    {
        var delay = retryDelay ?? DefaultRetryDelay;

        if (!broker.IsAvailable)
            LogWaiting(logger);

        while (true)
        {
            var connection = await broker.WaitForConnectionAsync(ct);

            try
            {
                await setup(connection, ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogSetupFailed(logger, ex, delay.TotalSeconds);
            }

            await Task.Delay(delay, ct);
            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRetryDelay.Ticks));
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Queue consumer is waiting for the RabbitMQ connection.")]
    private static partial void LogWaiting(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Queue consumer setup failed, retrying in {DelaySeconds}s.")]
    private static partial void LogSetupFailed(ILogger logger, Exception ex, double delaySeconds);
}
