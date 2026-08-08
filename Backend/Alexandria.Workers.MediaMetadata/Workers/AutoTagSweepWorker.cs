using System.Text;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Services.Storage.AutoTagging;
using Alexandria.Workers.MediaMetadata.Queueing;
using Microsoft.Extensions.Options;

namespace Alexandria.Workers.MediaMetadata.Workers;

/// <summary>
/// Safety net for the auto-tag pipeline. Runs on a timer
/// (<c>Tagging:AutoTagSyncIntervalSeconds</c>) and does two jobs:
/// <list type="bullet">
/// <item><b>Backstop</b> — re-queues files whose enrichment committed but whose tag sync
/// was lost (worker crash between commit and enqueue) by pushing their file IDs into the
/// in-process <see cref="AutoTagQueueService"/>.</item>
/// <item><b>Failed retry</b> — re-publishes <c>media-metadata.enrich</c> for files whose
/// newest enrichment attempt is <c>Failed</c>/<c>MissingOutput</c> and older than
/// <c>Tagging:RetryCooldownHours</c> (default 24h), skipping client-encrypted files.
/// Throttled by the attempt cooldown — no per-file failure counters.</item>
/// </list>
/// Registered only while <c>Features:Autotagging</c> is enabled.
/// </summary>
public partial class AutoTagSweepWorker(
    ILogger<AutoTagSweepWorker> logger,
    IOptions<AutoTaggingOptions> taggingOptions,
    IServiceProvider serviceProvider,
    AutoTagQueueService queue,
    IPublisherService publisher) : BackgroundService
{
    private const string EnrichRoutingKey = "media-metadata.enrich";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = taggingOptions.Value;
        var interval = TimeSpan.FromSeconds(Math.Max(config.AutoTagSyncIntervalSeconds, 10));

        LogSweepStarted(logger, interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                await SweepAsync(scope, stoppingToken);
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

    private async Task SweepAsync(IServiceScope scope, CancellationToken ct)
    {
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var cooldown = TimeSpan.FromHours(Math.Max(taggingOptions.Value.RetryCooldownHours, 1));

        // Job A — backstop: recovery for files the in-process queue missed.
        var backstop = (await unitOfWork.EssentiaBatchFiles.GetAutoTagBackstopCandidatesAsync(ct)).ToList();
        foreach (var fileId in backstop)
        {
            await queue.QueueFileAsync(fileId, ct);
        }

        if (backstop.Count > 0)
            LogBackstopQueued(logger, backstop.Count);

        // Job B — failed retry: re-trigger enrichment past the cooldown.
        var cutoff = DateTime.UtcNow.Add(-cooldown);
        var retry = (await unitOfWork.EssentiaBatchFiles.GetAutoTagRetryCandidatesAsync(cutoff, ct)).ToList();
        foreach (var fileId in retry)
        {
            await publisher.PublishAsync(
                Encoding.UTF8.GetBytes(fileId.ToString()),
                EnrichRoutingKey);
        }

        if (retry.Count > 0)
            LogRetryQueued(logger, retry.Count);
    }
}