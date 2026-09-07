using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Cleanup;

// TODO(single-replica): this backfill assumes one API instance. Overlapping runs
// are benign today (same value written), but if the API is ever scaled horizontally,
// gate this with a Postgres advisory lock or a claimed-row marker.
public class PreviewSizeBackfillWorker(
    IServiceProvider serviceProvider,
    ILogger<PreviewSizeBackfillWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);

            using var scope = serviceProvider.CreateScope();
            var backfill = scope.ServiceProvider.GetRequiredService<PreviewSizeBackfillService>();
            var result = await backfill.BackfillAsync(stoppingToken);

            logger.LogInformation(
                "Preview size backfill finished: {Backfilled} backfilled, {Missing} missing in storage, " +
                "{Skipped} without key, {Failed} failed, {Scanned} scanned",
                result.Backfilled, result.Missing, result.Skipped, result.Failed, result.Scanned);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Preview size backfill stopped before completion");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Preview size backfill failed");
        }
    }
}