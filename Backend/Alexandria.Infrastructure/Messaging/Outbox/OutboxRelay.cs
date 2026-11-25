using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Infrastructure.Messaging.Outbox;

/// <summary>
/// Sends committed outbox messages to the broker. Every host that publishes runs one: a commit in
/// the same process wakes it at once, and the poll picks up rows left by other hosts or a crash.
/// </summary>
public sealed partial class OutboxRelay(
    IServiceScopeFactory scopeFactory,
    OutboxSignal signal,
    IMessageBrokerStatus broker,
    IOptions<OutboxOptions> options,
    ILogger<OutboxRelay> logger) : BackgroundService
{
    private static readonly TimeSpan PruneInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!broker.IsEnabled)
        {
            LogBrokerDisabled(logger);
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(Math.Max(options.Value.PollIntervalSeconds, 1));
        var nextPruneAt = DateTime.MinValue;

        while (true)
        {
            try
            {
                // Scheduled before the prune runs, so a prune that keeps failing waits for its next
                // slot instead of skipping the send on every cycle.
                if (DateTime.UtcNow >= nextPruneAt)
                {
                    nextPruneAt = DateTime.UtcNow + PruneInterval;
                    await PruneAsync(stoppingToken);
                }

                // Sending during an outage would only fail; the rows simply wait.
                if (broker.IsAvailable)
                    await DrainAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                LogStopping(logger);
                return;
            }
            catch (Exception ex)
            {
                LogCycleFailed(logger, ex);
            }

            try
            {
                await signal.WaitAsync(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                LogStopping(logger);
                return;
            }
        }
    }

    private async Task DrainAsync(CancellationToken ct)
    {
        bool more;

        do
        {
            using var scope = scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
            more = await dispatcher.DispatchDueAsync(DateTime.UtcNow, ct);
        } while (more && !ct.IsCancellationRequested);
    }

    private async Task PruneAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();

        var pruned = await dispatcher.PruneSentAsync(DateTime.UtcNow, ct);
        if (pruned > 0)
            LogPruned(logger, pruned);
    }
}
