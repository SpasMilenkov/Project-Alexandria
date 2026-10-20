using Alexandria.Common.Services;
using Alexandria.Workers.Playlist.Config;
using Microsoft.Extensions.Options;

namespace Alexandria.Workers.Playlist.Workers;

/// <summary>
/// Periodic owner reconcile. Normal ticks only touch owners whose playlist inputs
/// changed; a full candidate pass at startup and every six hours reaps strays the
/// incremental path cannot see and repairs changes no timestamp can observe.
/// </summary>
public partial class PlaylistSweepWorker(
    ILogger<PlaylistSweepWorker> logger,
    IOptions<PlaylistConsumerConfig> consumerOptions,
    IServiceProvider serviceProvider) : BackgroundService
{
    // Unchanged owners are skipped between these passes. The full pass is the
    // repair backstop for changes no timestamp can observe, such as hard-deleted
    // file-tag links whose event never arrived.
    private static readonly TimeSpan FullSweepInterval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(consumerOptions.Value.SweepIntervalSeconds, 10));
        var lastFullSweepAt = DateTime.MinValue;

        LogSweepStarted(logger, interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            var forceFull = DateTime.UtcNow - lastFullSweepAt >= FullSweepInterval;

            try
            {
                using var scope = serviceProvider.CreateScope();
                var sweep = scope.ServiceProvider.GetRequiredService<IAutoPlaylistSweepService>();

                var result = await sweep.SweepAsync(forceFull, stoppingToken);

                if (forceFull)
                    lastFullSweepAt = DateTime.UtcNow;

                LogSweepDrained(logger, result.OwnersSwept, result.PlaylistsSynced, result.ItemsAdded);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogSweepError(logger, ex);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}