using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Infrastructure.Messaging;

/// <summary>
/// Retries the first broker connection forever with capped backoff. The client's automatic
/// recovery only covers connections that were established once, so this loop is what lets a
/// process boot without RabbitMQ and pick it up whenever it appears.
/// </summary>
public sealed partial class MessageBrokerSupervisor(
    MessageBrokerConnection broker,
    IConnectionFactory factory,
    ILogger<MessageBrokerSupervisor> logger) : BackgroundService
{
    internal TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(2);

    internal TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    internal TimeSpan ReminderInterval { get; init; } = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!broker.IsEnabled)
        {
            LogDisabled(logger);
            return;
        }

        var endpoint = Describe(factory);
        var delay = InitialRetryDelay;
        var attempt = 0;
        DateTime? lastWarningAt = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            attempt++;

            try
            {
                var connection = await factory.CreateConnectionAsync(stoppingToken);

                connection.ConnectionShutdownAsync += OnShutdownAsync;
                connection.RecoverySucceededAsync += OnRecoveredAsync;
                broker.SetConnection(connection);

                LogConnected(logger, endpoint, attempt);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                var now = DateTime.UtcNow;

                if (lastWarningAt is null)
                {
                    LogUnavailable(logger, endpoint, ex.Message);
                    lastWarningAt = now;
                }
                else if (now - lastWarningAt >= ReminderInterval)
                {
                    LogStillUnavailable(logger, endpoint, attempt, ex.Message);
                    lastWarningAt = now;
                }
                else
                {
                    LogAttemptFailed(logger, endpoint, attempt, delay.TotalSeconds);
                }
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            delay = TimeSpan.FromTicks(Math.Min((long)(delay.Ticks * 1.5), MaxRetryDelay.Ticks));
        }
    }

    private Task OnShutdownAsync(object sender, ShutdownEventArgs args)
    {
        if (args.Initiator != ShutdownInitiator.Application)
            LogConnectionLost(logger, args.ReplyText);

        return Task.CompletedTask;
    }

    private Task OnRecoveredAsync(object sender, AsyncEventArgs args)
    {
        LogRecovered(logger);
        return Task.CompletedTask;
    }

    private static string Describe(IConnectionFactory factory)
    {
        if (factory is ConnectionFactory concrete)
            return $"{concrete.HostName}:{concrete.Port}";

        return "configured broker";
    }
}
