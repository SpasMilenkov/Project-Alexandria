using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Helpers;
using Alexandria.Common.Processes;
using Alexandria.Common.Services;
using Microsoft.Extensions.Options;

namespace Alexandria.Workers.Document;

/// <summary>
/// Fills in document metadata for files that never went through a preview job with extraction,
/// and for rows written by an older extractor. Each document is downloaded whole, because
/// pdfinfo reads a local file.
/// </summary>
public partial class DocumentMetadataSweepWorker(
    ILogger<DocumentMetadataSweepWorker> logger,
    IOptions<DocumentMetadataSweepConfig> options,
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

        var candidates = await unitOfWork.DocumentMetadata.GetExtractionCandidatesAsync(
            DocumentMetadataFileTypes.Supported.Types,
            DocumentMetadataExtractor.CurrentVersion,
            Math.Max(options.Value.BatchSize, 1),
            _failedVersions,
            ct);

        if (candidates.Count == 0)
        {
            ResetExpiredFailures();
            return 0;
        }

        var storage = scope.ServiceProvider.GetRequiredService<IContentStorageService>();
        var documentMetadata = scope.ServiceProvider.GetRequiredService<IDocumentMetadataService>();
        var stored = 0;

        foreach (var candidate in candidates)
        {
            var scratch = JobScratch.Create();

            try
            {
                var localPath = Path.Combine(scratch, "source");
                await using (var file = File.Create(localPath))
                {
                    await storage.StreamFile(candidate.VersionId, file, ct);
                }

                await documentMetadata.ExtractAndStoreAsync(candidate.VersionId, candidate.MimeType, localPath, ct);
                stored++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _failedVersions.Add(candidate.VersionId);
                LogVersionFailed(logger, ex, candidate.VersionId);
            }
            finally
            {
                JobScratch.Delete(scratch, logger);
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
