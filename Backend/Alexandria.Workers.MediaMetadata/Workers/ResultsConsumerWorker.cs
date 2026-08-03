using System.Text;
using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Workers.MediaMetadata.Config;
using Alexandria.Workers.MediaMetadata.Messages;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace Alexandria.Workers.MediaMetadata.Workers;

/// <summary>
/// Consumes <c>media-metadata-results-queue</c> (completion messages), reads the
/// per-file JSON outputs, upserts <c>essentia-genre-{backbone}</c> + <c>essentia-mood</c>
/// enrichment rows (idempotent on <c>(FileId, Analyzer, Version)</c>), marks the batch
/// <c>Completed</c> and cleans up staged/output files. Acks only after the batch is
/// fully persisted.
/// </summary>
public partial class ResultsConsumerWorker(
    ILogger<ResultsConsumerWorker> logger,
    IConnection connection,
    IServiceProvider serviceProvider,
    IOptions<RabbitMqConsumerConfig> rabbitOptions,
    IOptions<EssentiaConfig> essentiaOptions) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = rabbitOptions.Value;
        var exchangeName = config.ExchangeName;
        var queueName = config.ResultsQueueName;

        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(0, config.PrefetchCount, false, stoppingToken);

        await _channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: "media-metadata.completed.#",
            arguments: null,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();

            try
            {
                var completion = Deserialize(body);
                if (completion is null || completion.BatchId == Guid.Empty)
                {
                    LogMalformedMessage(logger, Encoding.UTF8.GetString(body));
                    await _channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: false, stoppingToken);
                    return;
                }

                await HandleCompletionAsync(completion, stoppingToken);
                await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
            }
            catch (Exception ex)
            {
                LogProcessError(logger, ex);
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: true, stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        LogConsumerStarted(logger, queueName, exchangeName);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleCompletionAsync(CompletionMessage completion, CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var batch = await unitOfWork.EssentiaBatches.GetWithFilesAsync(completion.BatchId, ct);
        if (batch is null)
        {
            LogBatchNotFound(logger, completion.BatchId);
            return;
        }

        if (batch.Status == EssentiaBatchStatus.Completed)
        {
            LogBatchAlreadyCompleted(logger, completion.BatchId);
            return;
        }

        var config = essentiaOptions.Value;
        var localOutputDir = completion.OutputDir is null ? null : config.ToLocalPath(completion.OutputDir);

        foreach (var batchFile in batch.Files)
        {
            if (localOutputDir is null || !Directory.Exists(localOutputDir))
            {
                batchFile.Status = EssentiaBatchFileStatus.MissingOutput;
                LogMissingOutput(logger, batchFile.FileId, completion.BatchId);
                continue;
            }

            var jsonPath = Path.Combine(localOutputDir, $"{batchFile.FileId}.json");
            if (!File.Exists(jsonPath))
            {
                batchFile.Status = EssentiaBatchFileStatus.MissingOutput;
                LogMissingOutput(logger, batchFile.FileId, completion.BatchId);
                continue;
            }

            PerFileOutput? output;
            try
            {
                output = JsonSerializer.Deserialize<PerFileOutput>(await File.ReadAllTextAsync(jsonPath, ct),
                    JsonOptions);
            }
            catch (JsonException)
            {
                output = null;
            }

            if (output is null)
            {
                batchFile.Status = EssentiaBatchFileStatus.MissingOutput;
                LogMalformedPerFile(logger, batchFile.FileId, completion.BatchId);
                continue;
            }

            var backbone = !string.IsNullOrWhiteSpace(output.Backbone)
                ? output.Backbone
                : completion.Backbone ?? batch.Backbone.ToString().ToLowerInvariant();

            if (output.Success)
            {
                await UpsertSuccessAsync(unitOfWork, batchFile.FileId, backbone, output, ct);
                batchFile.Status = EssentiaBatchFileStatus.Succeeded;
                LogFileSucceeded(logger, batchFile.FileId, completion.BatchId);
            }
            else
            {
                var error = string.IsNullOrWhiteSpace(output.Error) ? "unknown error" : output.Error;
                await UpsertFailureAsync(unitOfWork, batchFile.FileId, backbone, output, error, ct);
                batchFile.Status = EssentiaBatchFileStatus.Failed;
                batchFile.ErrorDetail = error;
                LogFileFailed(logger, batchFile.FileId, completion.BatchId, error);
            }
        }

        batch.Status = EssentiaBatchStatus.Completed;
        batch.CompletedAt = DateTime.UtcNow;
        batch.UpdatedAt = DateTime.UtcNow;
        batch.UpdatedBy = SystemConfig.SystemId;

        await unitOfWork.SaveChangesAsync(ct);

        Cleanup(batch, config, localOutputDir);

        LogBatchCompleted(logger, completion.BatchId, batch.Files.Count);
    }

    private static async Task UpsertSuccessAsync(
        IUnitOfWork unitOfWork,
        Guid fileId,
        string backbone,
        PerFileOutput output,
        CancellationToken ct)
    {
        if (output.Genre is not null && !string.IsNullOrWhiteSpace(output.ModelVersions?.Genre))
        {
            await unitOfWork.FileEnrichments.UpsertAsync(new FileEnrichment
            {
                Id = Guid.NewGuid(),
                FileId = fileId,
                Analyzer = $"essentia-genre-{backbone.ToLowerInvariant()}",
                Version = output.ModelVersions.Genre,
                PayloadJson = JsonSerializer.Serialize(output.Genre, JsonOptions),
                UpdatedBy = SystemConfig.SystemId,
            }, ct);
        }

        if (output.Mood is not null && !string.IsNullOrWhiteSpace(output.ModelVersions?.Mood))
        {
            await unitOfWork.FileEnrichments.UpsertAsync(new FileEnrichment
            {
                Id = Guid.NewGuid(),
                FileId = fileId,
                Analyzer = "essentia-mood",
                Version = output.ModelVersions.Mood,
                PayloadJson = JsonSerializer.Serialize(output.Mood, JsonOptions),
                UpdatedBy = SystemConfig.SystemId,
            }, ct);
        }
    }

    private static async Task UpsertFailureAsync(
        IUnitOfWork unitOfWork,
        Guid fileId,
        string backbone,
        PerFileOutput output,
        string error,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { success = false, error }, JsonOptions);

        var genreVersion = !string.IsNullOrWhiteSpace(output.ModelVersions?.Genre)
            ? output.ModelVersions.Genre
            : "unknown";
        await unitOfWork.FileEnrichments.UpsertAsync(new FileEnrichment
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            Analyzer = $"essentia-genre-{backbone.ToLowerInvariant()}",
            Version = genreVersion,
            PayloadJson = payload,
            UpdatedBy = SystemConfig.SystemId,
        }, ct);

        var moodVersion = !string.IsNullOrWhiteSpace(output.ModelVersions?.Mood)
            ? output.ModelVersions.Mood
            : "unknown";
        await unitOfWork.FileEnrichments.UpsertAsync(new FileEnrichment
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            Analyzer = "essentia-mood",
            Version = moodVersion,
            PayloadJson = payload,
            UpdatedBy = SystemConfig.SystemId,
        }, ct);
    }

    private void Cleanup(EssentiaBatch batch, EssentiaConfig config, string? localOutputDir)
    {
        foreach (var batchFile in batch.Files)
        {
            try
            {
                foreach (var stagedFile in Directory.EnumerateFiles(config.StagedDir, $"{batchFile.FileId}.*"))
                    File.Delete(stagedFile);
            }
            catch (Exception ex)
            {
                LogCleanupError(logger, ex, config.StagedDir);
            }
        }

        if (localOutputDir is not null && Directory.Exists(localOutputDir))
        {
            try
            {
                Directory.Delete(localOutputDir, recursive: true);
            }
            catch (Exception ex)
            {
                LogCleanupError(logger, ex, localOutputDir);
            }
        }
    }

    private static CompletionMessage? Deserialize(byte[] body)
    {
        try
        {
            return JsonSerializer.Deserialize<CompletionMessage>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        LogWorkerStopping(logger);

        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}