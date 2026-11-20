using System.ComponentModel;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Helpers;
using Alexandria.Common.Processes;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Dto.Files;
using Microsoft.Extensions.Options;
using File = System.IO.File;

namespace Alexandria.Workers.Document;

/// <summary>
/// Reads the body text of documents for full-text search, newest uploads first. It is both the
/// path for new uploads and the backfill for existing ones, because it only looks at what the
/// database still lacks.
/// <para>
/// A failure that belongs to one document is recorded on its row and retried later. A failure
/// that would hit every document (storage unreachable, a tool missing from the image) stops the
/// batch instead, so documents do not use up their attempts on it, and is reported to monitoring.
/// </para>
/// </summary>
public partial class DocumentTextSweepWorker(
    ILogger<DocumentTextSweepWorker> logger,
    IOptions<DocumentTextSweepConfig> options,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    internal const string ServiceInstance = "document-text";

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

                var processed = await SweepBatchAsync(stoppingToken);
                delay = processed > 0 ? batchDelay : idleDelay;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogCycleFailed(logger, ex);
                await ReportCycleFailureAsync(ex, stoppingToken);
                delay = idleDelay;
            }
        }
    }

    /// <summary>
    /// Processes one batch and returns how many documents got a row, failed ones included.
    /// </summary>
    /// <exception cref="InvalidOperationException">No document in the batch could be downloaded.</exception>
    /// <exception cref="Win32Exception">An extraction tool could not be started.</exception>
    internal async Task<int> SweepBatchAsync(CancellationToken ct)
    {
        var config = options.Value;

        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var candidates = await unitOfWork.DocumentText.GetExtractionCandidatesAsync(
            DocumentTextFileTypes.Supported.Types,
            DocumentTextExtractor.CurrentVersion,
            DateTime.UtcNow.AddHours(-Math.Max(config.RetryFailedAfterHours, 0)),
            Math.Max(config.MaxAttempts, 1),
            Math.Max(config.BatchSize, 1),
            ct);

        if (candidates.Count == 0)
            return 0;

        var storage = scope.ServiceProvider.GetRequiredService<IContentStorageService>();
        var extractor = scope.ServiceProvider.GetRequiredService<IDocumentTextService>();

        var downloadFailures = new List<(DocumentTextCandidate Candidate, Exception Error)>();
        var processed = 0;

        foreach (var candidate in candidates)
        {
            var scratch = JobScratch.Create();

            try
            {
                var localPath = Path.Combine(scratch, "source");

                if (!await TryDownloadAsync(storage, candidate, localPath, downloadFailures, ct))
                    continue;

                await ExtractAndStoreAsync(unitOfWork, extractor, candidate, localPath, ct);
                processed++;
            }
            finally
            {
                JobScratch.Delete(scratch, logger);
            }
        }

        // When nothing downloads, storage is the likely cause rather than the documents.
        if (downloadFailures.Count == candidates.Count)
        {
            throw new InvalidOperationException(
                $"None of the {candidates.Count} document(s) in the batch could be downloaded: "
                + downloadFailures[0].Error.Message,
                downloadFailures[0].Error);
        }

        foreach (var (candidate, error) in downloadFailures)
        {
            await RecordFailureAsync(unitOfWork, candidate, $"Download failed: {error.Message}", ct);
            processed++;
        }

        LogBatchCompleted(logger, processed, candidates.Count);
        return processed;
    }

    private async Task<bool> TryDownloadAsync(
        IContentStorageService storage,
        DocumentTextCandidate candidate,
        string localPath,
        List<(DocumentTextCandidate, Exception)> failures,
        CancellationToken ct)
    {
        try
        {
            await using var file = File.Create(localPath);
            await storage.StreamFile(candidate.VersionId, file, ct);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            LogDownloadFailed(logger, ex, candidate.ContentObjectId, candidate.VersionId);
            failures.Add((candidate, ex));

            return false;
        }
    }

    private async Task ExtractAndStoreAsync(
        IUnitOfWork unitOfWork,
        IDocumentTextService extractor,
        DocumentTextCandidate candidate,
        string localPath,
        CancellationToken ct)
    {
        DocumentTextResult result;

        try
        {
            result = await extractor.ExtractAsync(candidate.MimeType, localPath, ct);
        }
        catch (Win32Exception)
        {
            // The tool itself could not start, so every document of this type would fail the same way.
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            LogExtractionFailed(logger, ex, candidate.ContentObjectId);
            await RecordFailureAsync(unitOfWork, candidate, ex.Message, ct);
            return;
        }

        var truncated = await unitOfWork.DocumentText.UpsertAsync(new DocumentText
        {
            Id = Guid.NewGuid(),
            ContentObjectId = candidate.ContentObjectId,
            Status = result.Status,
            Content = result.Text,
            CharacterCount = result.Text.Length,
            IsTruncated = result.IsTruncated,
            ExtractorVersion = DocumentTextExtractor.CurrentVersion
        }, ct);

        LogStored(logger, candidate.ContentObjectId, result.Status.ToString(), result.Text.Length, truncated);
    }

    private static Task RecordFailureAsync(
        IUnitOfWork unitOfWork,
        DocumentTextCandidate candidate,
        string errorDetail,
        CancellationToken ct)
        => unitOfWork.DocumentText.RecordFailureAsync(
            candidate.ContentObjectId, DocumentTextExtractor.CurrentVersion, errorDetail, ct);

    // Runs in a fresh scope, because the failed cycle may have left its own context unusable.
    private async Task ReportCycleFailureAsync(Exception failure, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await WorkerCycleFailureRecorder.RecordAsync(
                unitOfWork, ServiceType.DocumentPreviews, ServiceInstance, failure, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Usually the database itself is down, which its own health check already reports.
            LogCycleFailureNotRecorded(logger, ex);
        }
    }
}