using System.Text;
using Alexandria.Infrastructure.Messaging;
using Alexandria.Workers.Document.Handlers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Workers.Document;

public class Worker(
    ILogger<Worker> logger,
    MessageBrokerConnection broker,
    IConfiguration configuration,
    IServiceProvider serviceProvider)
    : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConsumerStartup.RunAsync(broker, StartConsumingAsync, logger, ct: stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task StartConsumingAsync(IConnection connection, CancellationToken stoppingToken)
    {
        logger.LogInformation("RabbitMQ Worker starting...");
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        var exchangeName = "content-exchange";
        var routingKey = "document.#";
        var prefetchCount = configuration.GetValue<ushort>("RabbitMQ:Consumer:PrefetchCount", 10);
        await _channel.BasicQosAsync(0, prefetchCount, false, stoppingToken);
        await _channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);
        var queueName = configuration.GetValue<string>("RabbitMQ:Consumer:QueueName", "document-queue");
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
            routingKey: routingKey,
            arguments: null,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (sender, eventArgs) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var messageHandler = scope.ServiceProvider.GetRequiredService<IPreviewGenerationHandler>();
                var body = eventArgs.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                logger.LogInformation("Received message: {Message}", message);

                await messageHandler.HandleAsync(message, stoppingToken);

                await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Left unacked on purpose: a nack here would drop the message.
                logger.LogInformation(
                    "Stopping mid-message; delivery {DeliveryTag} goes back to the queue when the channel closes.",
                    eventArgs.DeliveryTag);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message");
                await _channel.BasicNackAsync(
                    eventArgs.DeliveryTag,
                    false,
                    requeue: false,
                    stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation("RabbitMQ Worker started and listening on queue: {QueueName}", queueName);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("RabbitMQ Worker stopping...");

        // Handlers have to be told to stop first: closing the channel waits for the running one.
        await base.StopAsync(cancellationToken);

        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }
    }
}