using Alexandria.Common.Services;
using Alexandria.Workers.Playlist.Config;
using Microsoft.Extensions.Options;

namespace Alexandria.Workers.Playlist.Workers;

/// <summary>
/// Full owner-wide reconcile on a timer. Fills the sweep worker's call site:
/// materializes playlists for groupings no file event ever created (libraries that
/// predate the pipeline) and reaps strays the incremental path cannot see.
/// </summary>
public partial class PlaylistSweepWorker(
    ILogger<PlaylistSweepWorker> logger,
    IOptions<PlaylistConsumerConfig> consumerOptions,
    IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(consumerOptions.Value.SweepIntervalSeconds, 10));

        LogSweepStarted(logger, interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var sweep = scope.ServiceProvider.GetRequiredService<IAutoPlaylistSweepService>();

                var result = await sweep.SweepAsync(stoppingToken);
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