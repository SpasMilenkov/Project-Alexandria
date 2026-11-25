using System.Text;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Infrastructure.Messaging;
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
    MessageBrokerConnection broker,
    IServiceProvider serviceProvider,
    IOptions<RabbitMqConsumerConfig> rabbitOptions,
    AccumulatorBuffer buffer,
    DeliveryRetry deliveryRetry) : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConsumerStartup.RunAsync(broker, StartConsumingAsync, logger, ct: stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task StartConsumingAsync(IConnection connection, CancellationToken stoppingToken)
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

            if (buffer.IsPending(fileId))
            {
                LogDuplicateTrigger(logger, fileId);
                await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
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
                    await DeliverySettlement.AckAsync(_channel, eventArgs.DeliveryTag, logger, stoppingToken);
                    return;
                }

                var deliveryTag = eventArgs.DeliveryTag;
                var channel = _channel!;

                // Acked only once the accumulator has persisted the batch; until then a crash
                // leaves the trigger unacked so the broker redelivers it.
                Task Settle(bool persisted) => persisted
                    ? channel.BasicAckAsync(deliveryTag, false, CancellationToken.None).AsTask()
                    : channel.BasicNackAsync(deliveryTag, false, requeue: true, CancellationToken.None).AsTask();

                switch (buffer.TryEnqueue(staged, Settle))
                {
                    case EnqueueResult.Added:
                        LogTriggerQueued(logger, fileId, staged.FilePath);
                        break;

                    case EnqueueResult.Duplicate:
                        LogDuplicateTrigger(logger, fileId);
                        staging.Discard(staged);
                        await DeliverySettlement.AckAsync(_channel, eventArgs.DeliveryTag, logger, stoppingToken);
                        break;

                    case EnqueueResult.BufferFull:
                        LogBufferFull(logger, fileId);
                        staging.Discard(staged);
                        await DeliverySettlement.NackAsync(_channel, eventArgs.DeliveryTag, requeue: true, logger,
                            stoppingToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Not a failed attempt: the retry path would count it towards parking.
                LogStoppingMidMessage(logger, eventArgs.DeliveryTag);
            }
            catch (Exception ex)
            {
                // Staging failed before any batch or Job row existed, so the monitoring
                // dashboards have nothing to count here. The counted redelivery parks poison
                // triggers instead of looping; the short pause keeps a storage outage from
                // burning every attempt in seconds.
                LogStageError(logger, ex, fileId);

                await deliveryRetry.HandleFailureAsync(
                    _channel,
                    eventArgs,
                    new DeliveryRetryOptions
                    {
                        MaxAttempts = config.TriggerMaxStagingAttempts,
                        RetryDelay = TimeSpan.FromSeconds(Math.Max(config.TriggerRetryDelaySeconds, 0))
                    },
                    async attempt =>
                    {
                        LogTriggerParked(logger, ex, fileId, attempt);
                        await deliveryRetry.RepublishAsync(
                            eventArgs, config.TriggerParkingRoutingKey, attempt, stoppingToken);
                        await RecordTriggerFailureAsync(serviceProvider, ex, logger, stoppingToken);
                    },
                    stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        LogConsumerStarted(logger, queueName, exchangeName);
    }

    // Runs after the trigger is already in the parking queue. Throwing here would requeue the
    // original, and its next failure would park a second copy.
    private static async Task RecordTriggerFailureAsync(
        IServiceProvider serviceProvider, Exception ex, ILogger logger, CancellationToken ct)
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
            LogParkRecordFailed(logger, recordEx);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        LogWorkerStopping(logger);

        // Handlers have to be told to stop first: closing the channel waits for the running one.
        await base.StopAsync(cancellationToken);

        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }
    }
}