using Alexandria.Services.Storage.AutoTagging;
using Alexandria.Workers.MediaMetadata.Queueing;

namespace Alexandria.Workers.MediaMetadata.Workers;

/// <summary>
/// Drains the in-process <see cref="AutoTagQueueService"/> and applies auto-tags to each
/// file on its own scoped unit of work. Registered only while <c>Features:Autotagging</c>
/// is enabled. Tag-sync failures are logged and never abort the drain.
/// </summary>
public partial class AutoTagSyncWorker(
    IServiceProvider serviceProvider,
    AutoTagQueueService queue,
    ILogger<AutoTagSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);

        await foreach (var fileId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<IAutoTagSyncService>();
                await syncService.SyncFileAsync(fileId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogSyncError(logger, ex, fileId);
            }
        }

        LogStopped(logger);
    }
}