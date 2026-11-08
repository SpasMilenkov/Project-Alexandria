using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Microsoft.Extensions.Options;

namespace Alexandria.Workers.Media;

/// <summary>
/// Fills in capture metadata for images that never went through a preview job with extraction,
/// and for rows written by an older extractor. Storage reads are ranged, so each image only
/// costs its header rather than a full download.
/// </summary>
public partial class CaptureMetadataSweepWorker(
    ILogger<CaptureMetadataSweepWorker> logger,
    IOptions<CaptureMetadataSweepConfig> options,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    // A corrupt file fails every time, so failures sit out instead of blocking the batch forever.
    private readonly HashSet<Guid> _failedVersions = [];
    private DateTime _failuresResetAt = DateTime.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        if (!config.Enabled)
        {
            LogDisabled(logger);
            return;
        }

        var batchDelay = TimeSpan.FromSeconds(Math.Max(config.BatchDelaySeconds, 1));
        var idleDelay = TimeSpan.FromSeconds(Math.Max(config.IdleDelaySeconds, 1));
        var delay = batchDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, stoppingToken);

                var stored = await SweepBatchAsync(stoppingToken);
                delay = stored > 0 ? batchDelay : idleDelay;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogSweepFailed(logger, ex);
                delay = idleDelay;
            }
        }
    }

    /// <summary>
    /// Processes one batch and returns how many versions were stored.
    /// </summary>
    internal async Task<int> SweepBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var candidates = await unitOfWork.CaptureMetadata.GetExtractionCandidatesAsync(
            CaptureMetadataExtractor.CurrentVersion,
            Math.Max(options.Value.BatchSize, 1),
            _failedVersions,
            ct);

        if (candidates.Count == 0)
        {
            ResetExpiredFailures();
            return 0;
        }

        var storage = scope.ServiceProvider.GetRequiredService<IContentStorageService>();
        var captureMetadata = scope.ServiceProvider.GetRequiredService<ICaptureMetadataService>();
        var stored = 0;

        foreach (var versionId in candidates)
        {
            try
            {
                await using var source = await storage.DownloadSeekableFile(versionId, ct);
                await captureMetadata.ExtractAndStoreAsync(versionId, source, ct);
                stored++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _failedVersions.Add(versionId);
                LogVersionFailed(logger, ex, versionId);
            }
        }

        LogBatchCompleted(logger, stored, candidates.Count);
        return stored;
    }

    private void ResetExpiredFailures()
    {
        var retryAfter = TimeSpan.FromHours(Math.Max(options.Value.RetryFailedAfterHours, 0));
        if (_failedVersions.Count == 0 || DateTime.UtcNow - _failuresResetAt < retryAfter)
            return;

        LogRetryingFailures(logger, _failedVersions.Count);
        _failedVersions.Clear();
        _failuresResetAt = DateTime.UtcNow;
    }
}
