using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Workers.MediaMetadata.Config;
using Microsoft.Extensions.Options;

namespace Alexandria.Workers.MediaMetadata.Workers;

/// <summary>
/// Periodically marks <c>Dispatched</c> batches that exceeded the backbone timeout as
/// <c>TimedOut</c>, transitions their still-<c>Pending</c> batch-files to <c>Failed</c>
/// and writes failure-visible <c>FileEnrichment</c> rows. Runs once at startup to
/// re-check batches that were left dispatched while the worker was down.
/// </summary>
public partial class TimeoutSweepWorker(
    ILogger<TimeoutSweepWorker> logger,
    IOptions<EssentiaConfig> essentiaOptions,
    IServiceProvider serviceProvider) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = essentiaOptions.Value;
        var interval = TimeSpan.FromSeconds(Math.Max(config.SweepIntervalSeconds, 10));

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
                await RecordSweepFailureAsync(ex, stoppingToken);
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

    internal async Task SweepAsync(IServiceScope scope, CancellationToken ct)
    {
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var config = essentiaOptions.Value;

        var timeoutSeconds = config.Backbone.Equals("maest", StringComparison.OrdinalIgnoreCase)
            ? config.MaestTimeoutSeconds
            : config.EffnetTimeoutSeconds;

        var cutoff = DateTime.UtcNow.AddSeconds(-timeoutSeconds);
        var overdue = (await unitOfWork.EssentiaBatches.GetDispatchedSinceAsync(cutoff, ct)).ToList();
        if (overdue.Count == 0)
            return;

        var batchIds = overdue.Select(b => b.Id).ToList();
        var timedOut = await unitOfWork.EssentiaBatches.MarkTimedOutAsync(batchIds, ct);

        LogBatchesTimedOut(logger, timedOut, batchIds.Count);

        foreach (var batch in overdue)
        {
            var files = await unitOfWork.EssentiaBatchFiles.GetByBatchAsync(batch.Id, ct);
            var pendingFiles = files.Where(f => f.Job.Status == JobStatus.Queued).ToList();
            if (pendingFiles.Count == 0)
                continue;

            foreach (var file in pendingFiles)
                await UpsertTimeoutFailureAsync(unitOfWork, file.FileId, batch.Backbone, batch.Id, ct);

            await unitOfWork.Jobs.UpdateStatusForJobsAsync(
                pendingFiles.Select(f => f.JobId), JobStatus.Failed, "batch timed out", ct);

            LogBatchFilesTimedOut(logger, batch.Id, pendingFiles.Count);
        }
    }

    private async Task RecordSweepFailureAsync(Exception ex, CancellationToken ct)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await WorkerCycleFailureRecorder.RecordAsync(unitOfWork, ServiceType.MediaMetadata, "media-metadata", ex,
                ct);
        }
        catch (Exception loggingEx)
        {
            LogSweepError(logger, loggingEx);
        }
    }

    private static async Task UpsertTimeoutFailureAsync(
        IUnitOfWork unitOfWork, Guid fileId, EssentiaBackbone backbone, Guid batchId, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(
            new { success = false, error = "batch timed out", batch_id = batchId }, JsonOptions);

        await unitOfWork.FileEnrichments.UpsertAsync(new FileEnrichment
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            Analyzer = $"essentia-genre-{backbone.ToString().ToLowerInvariant()}",
            Version = "unknown",
            PayloadJson = payload,
            UpdatedBy = SystemConfig.SystemId,
        }, ct);

        await unitOfWork.FileEnrichments.UpsertAsync(new FileEnrichment
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            Analyzer = "essentia-mood",
            Version = "unknown",
            PayloadJson = payload,
            UpdatedBy = SystemConfig.SystemId,
        }, ct);
    }
}