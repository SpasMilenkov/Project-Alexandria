using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Cleanup;

// TODO(single-replica): this backfill assumes one API instance. Overlapping runs
// are benign today (same values written), but if the API is ever scaled horizontally,
// gate this with a Postgres advisory lock or a claimed-row marker.
public class RepresentationSizeBackfillWorker(
    IServiceProvider serviceProvider,
    ILogger<RepresentationSizeBackfillWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);

            using var scope = serviceProvider.CreateScope();
            var backfill = scope.ServiceProvider.GetRequiredService<RepresentationSizeBackfillService>();
            var result = await backfill.BackfillAsync(stoppingToken);

            logger.LogInformation(
                "Representation size backfill finished: {Backfilled} backfilled across {Jobs} jobs, " +
                "{Unmatched} without bytes, {Failed} failed, {Scanned} scanned",
                result.Backfilled, result.Jobs, result.Unmatched, result.Failed, result.Scanned);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Representation size backfill stopped before completion");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Representation size backfill failed");
        }
    }
}