using System.Text;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
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

        // Parking lot for poison triggers: messages that exhaust their staging
        // attempts land here instead of looping forever, and surface once as a
        // worker-cycle failure event (see the staging catch block below).
        await _channel.QueueDeclareAsync(
            queue: config.TriggerParkingQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue: config.TriggerParkingQueueName,
            exchange: exchangeName,
            routingKey: config.TriggerParkingRoutingKey,
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
                // Staging failed before any batch or Job row existed, so the
                // monitoring dashboards have nothing to count here. Count
                // deliveries via a header (plain requeue preserves nothing we
                // can increment) and park poison triggers instead of looping.
                var attempt = TriggerRetryPolicy.NextAttemptNumber(eventArgs.BasicProperties);
                if (TriggerRetryPolicy.ShouldPark(attempt, config.TriggerMaxStagingAttempts))
                {
                    LogTriggerParked(logger, ex, fileId, attempt);
                    try
                    {
                        await TriggerRetryPolicy.PublishWithAttemptsAsync(
                            _channel, exchangeName, config.TriggerParkingRoutingKey,
                            body, attempt, stoppingToken);
                        await RecordTriggerFailureAsync(serviceProvider, ex, stoppingToken);
                        await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
                    }
                    catch (Exception parkEx)
                    {
                        // Broker too sick to park: keep the message via requeue
                        // rather than dropping it on the floor.
                        LogParkFailed(logger, parkEx, fileId);
                        await _channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: true, stoppingToken);
                    }

                    return;
                }

                LogStageError(logger, ex, fileId);
                await TriggerRetryPolicy.PublishWithAttemptsAsync(
                    _channel, exchangeName, eventArgs.RoutingKey,
                    body, attempt, stoppingToken);
                await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
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

    private static async Task RecordTriggerFailureAsync(
        IServiceProvider serviceProvider, Exception ex, CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await WorkerCycleFailureRecorder.RecordAsync(unitOfWork, ServiceType.MediaMetadata, "media-metadata", ex, ct);
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