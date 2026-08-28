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
/// Drains the <see cref="AccumulatorBuffer"/>, flushes batches on
/// <c>BatchSize</c> or <c>MaxBatchWaitSeconds</c>, persists a
/// <c>Dispatched</c> batch with <c>Pending</c> batch-files and publishes the
/// dispatch message to the active backbone queue.
/// </summary>
public partial class AccumulatorWorker(
    ILogger<AccumulatorWorker> logger,
    AccumulatorBuffer buffer,
    IOptions<EssentiaConfig> essentiaOptions,
    IPublisherService publisher,
    IJobOutcomeTracker outcomeTracker,
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

            await FlushAsync(batch, backbone, routingKey, config, stoppingToken);
        }
    }

    private async Task FlushAsync(
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
                .Select(f => new EssentiaBatchFile
                {
                    FileId = f.FileId,
                    Status = EssentiaBatchFileStatus.Pending,
                    UpdatedBy = SystemConfig.SystemId,
                })
                .ToList()
        };

        try
        {
            await unitOfWork.EssentiaBatches.AddAsync(batchEntity, stoppingToken);
            await unitOfWork.SaveChangesAsync(stoppingToken);
            outcomeTracker.RecordSuccess(ServiceType.MediaMetadata);
        }
        catch (Exception ex)
        {
            outcomeTracker.RecordFailure(ServiceType.MediaMetadata);
            LogPersistError(logger, ex, batchId, batch.Count);
            return;
        }

        var outputDir = $"{config.ToVolumePath(config.OutputDir.TrimEnd('/'))}/{batchId}";
        var files = batch.Select(f => config.ToVolumePath(f.FilePath)).ToArray();

        foreach (var staged in batch)
            buffer.MarkDispatched(staged.FileId);

        var request = new ClassifyRequest
        {
            BatchId = batchId,
            OutputDir = outputDir,
            Files = files,
        };

        try
        {
            await publisher.PublishAsync(JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions), routingKey);
        }
        catch (Exception ex)
        {
            outcomeTracker.RecordFailure(ServiceType.MediaMetadata);
            LogDispatchError(logger, ex, batchId, batch.Count);
            return;
        }

        LogBatchDispatched(logger, batchId, backbone, batch.Count, outputDir, routingKey);
    }

    private static EssentiaBackbone ParseBackbone(string backbone) => backbone.ToLowerInvariant() switch
    {
        "effnet" => EssentiaBackbone.Effnet,
        "maest" => EssentiaBackbone.Maest,
        _ => throw new InvalidOperationException($"Unknown Essentia backbone: {backbone}"),
    };
}