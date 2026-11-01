using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Workers.MediaMetadata.Config;
using Alexandria.Workers.MediaMetadata.Messages;
using Alexandria.Workers.MediaMetadata.Queueing;
using Alexandria.Workers.MediaMetadata.Services;
using Microsoft.Extensions.Options;

namespace Alexandria.Workers.MediaMetadata.Workers;

/// <summary>
///     Drains the <see cref="AccumulatorBuffer" />, flushes batches on
///     <c>BatchSize</c> or <c>MaxBatchWaitSeconds</c>, persists a
///     <c>Dispatched</c> batch with <c>Pending</c> batch-files and publishes the
///     dispatch message to the active backbone queue.
/// </summary>
public partial class AccumulatorWorker(
    ILogger<AccumulatorWorker> logger,
    AccumulatorBuffer buffer,
    IOptions<EssentiaConfig> essentiaOptions,
    IPublisherService publisher,
    IServiceProvider serviceProvider) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = essentiaOptions.Value;
        var backbone = ParseBackbone(config.Backbone);
        var batchSize = Math.Max(config.BatchSize, 1);
        var wait = TimeSpan.FromSeconds(Math.Max(config.MaxBatchWaitSeconds, 1));
        var routingKey = $"media-metadata.classify.audio.{config.Backbone.ToLowerInvariant()}";

        LogAccumulatorStarted(logger, backbone, batchSize, wait, routingKey);

        while (!stoppingToken.IsCancellationRequested)
        {
            var batch = new List<StagedFile>();

            try
            {
                batch.Add(await buffer.Reader.ReadAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var flushAt = DateTime.UtcNow + wait;

            while (batch.Count < batchSize)
            {
                var remaining = flushAt - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    break;

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(remaining);

                try
                {
                    batch.Add(await buffer.Reader.ReadAsync(timeout.Token));
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            try
            {
                await FlushAsync(batch, backbone, routingKey, config, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A single bad flush must not stop the host and with it every future batch.
                LogFlushError(logger, ex, batch.Count);
            }
        }
    }

    internal async Task FlushAsync(
        List<StagedFile> batch,
        EssentiaBackbone backbone,
        string routingKey,
        EssentiaConfig config,
        CancellationToken stoppingToken)
    {
        var batchId = Guid.NewGuid();

        using var scope = serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var batchEntity = new EssentiaBatch
        {
            Id = batchId,
            Backbone = backbone,
            Status = EssentiaBatchStatus.Dispatched,
            DispatchedAt = DateTime.UtcNow,
            UpdatedBy = SystemConfig.SystemId,
            Files = batch
                .Select(f =>
                {
                    var jobId = Guid.NewGuid();
                    return new EssentiaBatchFile
                    {
                        FileId = f.FileId,
                        UpdatedBy = SystemConfig.SystemId,
                        JobId = jobId,
                        Job = new Job
                        {
                            Id = jobId,
                            Type = JobType.MetadataEnrichment,
                            Status = JobStatus.Queued,
                            UserId = SystemConfig.SystemId,
                            CreatedAt = DateTime.UtcNow
                        }
                    };
                })
                .ToList()
        };

        try
        {
            await unitOfWork.EssentiaBatches.AddAsync(batchEntity, stoppingToken);
            await unitOfWork.SaveChangesAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPersistError(logger, ex, batchId, batch.Count);

            // Release the files before anything else can fail: a leaked pending entry makes every
            // later trigger for the file a silent duplicate until the worker restarts.
            await SettleAsync(batch, persisted: false);

            // The failed context still tracks the unsaved batch, so it cannot record anything.
            await RecordCycleFailureAsync(ex, stoppingToken);
            return;
        }

        var outputDir = $"{config.ToVolumePath(config.OutputDir.TrimEnd('/'))}/{batchId}";
        var files = batch.Select(f => config.ToVolumePath(f.FilePath)).ToArray();

        await SettleAsync(batch, persisted: true);

        var request = new ClassifyRequest
        {
            BatchId = batchId,
            OutputDir = outputDir,
            Files = files
        };

        try
        {
            await publisher.PublishAsync(JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions), routingKey);
        }
        catch (Exception ex)
        {
            LogDispatchError(logger, ex, batchId, batch.Count);

            // Batch and per-file Job rows are already persisted, but the message that
            // would ever trigger their completion never went out. Without this they'd
            // sit in Queued forever, invisible to TimeoutSweepWorker since it
            // only looks at batches it believes were successfully dispatched.
            var jobIds = batchEntity.Files.Select(f => f.JobId).ToList();
            await unitOfWork.Jobs.UpdateStatusForJobsAsync(jobIds, JobStatus.Failed, "publish failed", stoppingToken);

            await WorkerCycleFailureRecorder.RecordAsync(unitOfWork, ServiceType.MediaMetadata, "media-metadata", ex,
                stoppingToken);
            return;
        }

        LogBatchDispatched(logger, batchId, backbone, batch.Count, outputDir, routingKey);
    }

    private async Task SettleAsync(List<StagedFile> batch, bool persisted)
    {
        foreach (var staged in batch)
        {
            buffer.MarkDispatched(staged.FileId);

            var settle = buffer.TakeSettler(staged.FileId);
            if (settle is null)
                continue;

            try
            {
                await settle(persisted);
            }
            catch (Exception ex)
            {
                // The channel may already be closed; the broker then redelivers the trigger itself.
                LogSettleError(logger, ex, staged.FileId);
            }
        }
    }

    private async Task RecordCycleFailureAsync(Exception ex, CancellationToken ct)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await WorkerCycleFailureRecorder.RecordAsync(unitOfWork, ServiceType.MediaMetadata, "media-metadata", ex,
                ct);
        }
        catch (Exception recordEx) when (recordEx is not OperationCanceledException)
        {
            LogRecordFailureError(logger, recordEx);
        }
    }

    private static EssentiaBackbone ParseBackbone(string backbone)
    {
        return backbone.ToLowerInvariant() switch
        {
            "effnet" => EssentiaBackbone.Effnet,
            "maest" => EssentiaBackbone.Maest,
            _ => throw new InvalidOperationException($"Unknown Essentia backbone: {backbone}")
        };
    }
}