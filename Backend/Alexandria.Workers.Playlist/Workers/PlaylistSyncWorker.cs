using Alexandria.Common.Playlists;
using Alexandria.Common.Services;
using Alexandria.Workers.Playlist.Config;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Workers.Playlist.Workers;

/// <summary>
/// Drains single-playlist rebuild requests. Parses and
/// acknowledges messages, but the reconciler call lands in.
/// Malformed bodies are discarded; transient failures requeue because
/// rebuilds are idempotent diffs.
/// </summary>
public partial class PlaylistSyncWorker(
    ILogger<PlaylistSyncWorker> logger,
    IConnection connection,
    IServiceProvider serviceProvider,
    IOptions<PlaylistConsumerConfig> consumerOptions) : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: false, stoppingToken);
                return;
            }

            try
            {
                using var scope = serviceProvider.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IAutoPlaylistSyncService>();

                var result = await sync.SyncFileAsync(fileId, ownerId, stoppingToken);
                LogFileSynced(logger, fileId, result.PlaylistsSynced, result.ItemsAdded);
                await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
            }
            catch (Exception ex)
            {
                LogSyncError(logger, ex, fileId);
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: true, stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: config.SyncQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        LogConsumerStarted(logger, config.SyncQueueName, config.ExchangeName);

        await Task.Delay(Timeout.Infinite, stoppingToken);
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