using System.Text;
using Alexandria.Workers.MediaMetadata.Config;
using Alexandria.Workers.MediaMetadata.Queueing;
using Alexandria.Workers.MediaMetadata.Services;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Workers.MediaMetadata.Workers;

/// <summary>
/// Consumes <c>media-metadata-trigger-queue</c> (raw Guid bodies, one per file),
/// stages the file and hands it off to the <see cref="AccumulatorBuffer"/> for
/// batched dispatch.
/// </summary>
public partial class TriggerConsumerWorker(
    ILogger<TriggerConsumerWorker> logger,
    IConnection connection,
    IServiceProvider serviceProvider,
    IOptions<RabbitMqConsumerConfig> rabbitOptions,
    AccumulatorBuffer buffer) : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = rabbitOptions.Value;
        var exchangeName = config.ExchangeName;
        var queueName = config.TriggerQueueName;

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
            routingKey: "media-metadata.enrich.#",
            arguments: null,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            if (!Guid.TryParse(message, out var fileId))
            {
                LogMalformedMessage(logger, message);
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: false, stoppingToken);
                return;
            }

            try
            {
                using var scope = serviceProvider.CreateScope();
                var staging = scope.ServiceProvider.GetRequiredService<IStagingService>();

                var staged = await staging.StageAsync(fileId, stoppingToken);
                if (staged is null)
                {
                    LogFileUnavailable(logger, fileId);
                    await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
                    return;
                }

                switch (buffer.TryEnqueue(staged))
                {
                    case EnqueueResult.Added:
                        LogTriggerQueued(logger, fileId, staged.FilePath);
                        await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
                        break;

                    case EnqueueResult.Duplicate:
                        LogDuplicateTrigger(logger, fileId);
                        await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
                        break;

                    case EnqueueResult.BufferFull:
                        LogBufferFull(logger, fileId);
                        await _channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: true, stoppingToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogStageError(logger, ex, fileId);
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