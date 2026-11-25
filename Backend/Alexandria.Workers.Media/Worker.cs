using System.Text;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Infrastructure.Messaging;
using Alexandria.Workers.Media.Handlers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Workers.Media;

public class Worker(
    ILogger<Worker> logger,
    MessageBrokerConnection broker,
    IConfiguration configuration,
    IServiceProvider serviceProvider,
    DeliveryRetry deliveryRetry)
    : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await ConsumerStartup.RunAsync(broker, StartConsumingAsync, logger, ct: ct);
        await Task.Delay(Timeout.Infinite, ct);
    }

    private async Task StartConsumingAsync(IConnection connection, CancellationToken ct)
    {
        logger.LogInformation("RabbitMQ Worker starting...");
        _channel = await connection.CreateChannelAsync(cancellationToken: ct);
        var exchangeName = "content-exchange";
        var routingKey = "media.#";
        var prefetchCount = configuration.GetValue<ushort>("RabbitMQ:Consumer:PrefetchCount", 10);
        await _channel.BasicQosAsync(0, prefetchCount, false, ct);
        await _channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);
        var queueName = configuration.GetValue<string>("RabbitMQ:Consumer:QueueName", "media-queue")!;
        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);
        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: routingKey,
            arguments: null,
            cancellationToken: ct);
        await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName,
            routingKey: "image.#", cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (sender, eventArgs) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                IPreviewGenerationHandler handler = eventArgs.RoutingKey.StartsWith("image.")
                    ? scope.ServiceProvider.GetRequiredService<ImagePreviewGenerationHandler>()
                    : scope.ServiceProvider.GetRequiredService<MediaPreviewGenerationHandler>();
                var message = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
                logger.LogInformation("Received message: {Message} with routing key: {RoutingKey}",
                    message, eventArgs.RoutingKey);

                await handler.HandleAsync(message, ct);

                await DeliverySettlement.AckAsync(_channel, eventArgs.DeliveryTag, logger, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Left unacked on purpose: a nack here would drop the message.
                logger.LogInformation(
                    "Stopping mid-message; delivery {DeliveryTag} goes back to the queue when the channel closes.",
                    eventArgs.DeliveryTag);
            }
            catch (Exception ex)
            {
                // The handlers record failures after the claim on the job itself, so what lands
                // here failed before any claim: retried a few times, then the job is failed so
                // automatic retry takes over instead of the message being dropped.
                logger.LogError(ex, "Error processing message");
                await deliveryRetry.HandleFailureAsync(
                    _channel, eventArgs, new DeliveryRetryOptions(), _ => ParkAsync(eventArgs, ex, ct), ct);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);
        logger.LogInformation("RabbitMQ Worker started and listening on queue: {QueueName}", queueName);
    }

    private async Task ParkAsync(BasicDeliverEventArgs eventArgs, Exception ex, CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await PreviewJobLifecycle.FailUnclaimedAsync(unitOfWork, eventArgs.Body.ToArray(), ct);

        try
        {
            await WorkerCycleFailureRecorder.RecordAsync(unitOfWork, ServiceType.MediaPreviews, "media", ex, ct);
        }
        catch (Exception recordEx) when (recordEx is not OperationCanceledException)
        {
            // The job is already failed and visible; the incident is extra signal.
            logger.LogWarning(recordEx, "Could not record the incident for a parked preview delivery");
        }
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        logger.LogInformation("RabbitMQ Worker stopping...");

        // Handlers have to be told to stop first: closing the channel waits for the running one.
        await base.StopAsync(ct);

        if (_channel != null)
        {
            await _channel.CloseAsync(ct);
            await _channel.DisposeAsync();
        }
    }
}