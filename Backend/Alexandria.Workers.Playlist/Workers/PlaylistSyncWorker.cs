using Alexandria.Common.Playlists;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Playlist;
using Alexandria.Infrastructure.Messaging;
using Alexandria.Workers.Playlist.Config;
using Alexandria.Workers.Playlist.Queueing;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Workers.Playlist.Workers;

/// <summary>
/// Consumes file-scoped playlist sync requests, coalesces them in process, and
/// applies one owner snapshot per flush. Messages stay unacknowledged until the
/// owner batch has been applied, so a crash redelivers the work.
/// </summary>
public partial class PlaylistSyncWorker(
    ILogger<PlaylistSyncWorker> logger,
    MessageBrokerConnection broker,
    IServiceProvider serviceProvider,
    IOptions<PlaylistConsumerConfig> consumerOptions,
    PlaylistSyncBuffer buffer) : BackgroundService
{
    private readonly SemaphoreSlim _channelGate = new(1, 1);
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConsumerStartup.RunAsync(broker, StartConsumingAsync, logger, ct: stoppingToken);
        await DrainAsync(consumerOptions.Value, stoppingToken);
    }

    private async Task StartConsumingAsync(IConnection connection, CancellationToken stoppingToken)
    {
        var config = consumerOptions.Value;

        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(0, config.PrefetchCount, false, stoppingToken);

        await _channel.ExchangeDeclareAsync(
            exchange: config.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: config.SyncQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue: config.SyncQueueName,
            exchange: config.ExchangeName,
            routingKey: $"{config.SyncRoutingKey}.#",
            arguments: null,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();

            if (!PlaylistSyncMessage.TryParse(body, out var fileId, out var ownerId))
            {
                LogMalformedMessage(logger);
                await NackAsync(eventArgs.DeliveryTag, requeue: false, stoppingToken);
                return;
            }

            switch (buffer.TryEnqueue(ownerId, fileId, eventArgs.DeliveryTag))
            {
                case EnqueueResult.Added:
                    LogFileQueued(logger, fileId);
                    break;

                case EnqueueResult.Duplicate:
                    LogDuplicateFileQueued(logger, fileId);
                    break;

                case EnqueueResult.BufferFull:
                    LogBufferFull(logger, fileId);
                    await NackAsync(eventArgs.DeliveryTag, requeue: true, stoppingToken);
                    break;
            }
        };

        await _channel.BasicConsumeAsync(
            queue: config.SyncQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        LogConsumerStarted(logger, config.SyncQueueName, config.ExchangeName);
    }

    private async Task DrainAsync(PlaylistConsumerConfig config, CancellationToken stoppingToken)
    {
        var maxBatchSize = Math.Max(config.MaxBatchSize, 1);
        var maxWait = TimeSpan.FromMilliseconds(
            Math.Max(config.CoalesceWindowMilliseconds, 0));

        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<PlaylistSyncWorkItem> batch;

            try
            {
                batch = await buffer.ReadBatchAsync(maxBatchSize, maxWait, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (batch.Count == 0)
                break;

            await ProcessBatchAsync(batch, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(
        IReadOnlyList<PlaylistSyncWorkItem> batch,
        CancellationToken ct)
    {
        foreach (var ownerBatch in batch.GroupBy(item => item.OwnerId))
        {
            var fileIds = ownerBatch
                .Select(item => item.FileId)
                .Distinct()
                .ToArray();

            var deliveryTags = ownerBatch
                .SelectMany(item => item.DeliveryTags)
                .ToArray();

            AutoPlaylistSyncResult result;

            try
            {
                using var scope = serviceProvider.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IAutoPlaylistSyncService>();

                result = await sync.SyncFilesAsync(ownerBatch.Key, fileIds, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogSyncError(logger, ex, ownerBatch.Key, fileIds.Length);

                foreach (var deliveryTag in deliveryTags)
                {
                    if (!await NackAsync(deliveryTag, requeue: true, ct))
                        break;
                }

                continue;
            }

            // Settled outside the sync's try: the sync is committed, so a lost ack is not a failed
            // sync. Once the channel is closed the broker requeues the rest of the batch anyway.
            foreach (var deliveryTag in deliveryTags)
            {
                if (!await AckAsync(deliveryTag, ct))
                    break;
            }

            LogBatchSynced(logger, ownerBatch.Key, fileIds.Length,
                result.PlaylistsSynced, result.ItemsAdded,
                result.ItemsRevived, result.ItemsRemoved);
        }
    }

    private async Task<bool> AckAsync(ulong deliveryTag, CancellationToken ct)
    {
        if (_channel is null)
            return false;

        await _channelGate.WaitAsync(ct);

        try
        {
            return await DeliverySettlement.AckAsync(_channel, deliveryTag, logger, ct);
        }
        finally
        {
            _channelGate.Release();
        }
    }

    private async Task<bool> NackAsync(ulong deliveryTag, bool requeue, CancellationToken ct)
    {
        if (_channel is null)
            return false;

        await _channelGate.WaitAsync(ct);

        try
        {
            return await DeliverySettlement.NackAsync(_channel, deliveryTag, requeue, logger, ct);
        }
        finally
        {
            _channelGate.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        LogWorkerStopping(logger);

        await base.StopAsync(cancellationToken);

        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }
    }
}