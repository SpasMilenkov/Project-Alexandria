using System.Text;
using Alexandria.Workers.Media.Handlers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Workers.Media;

public class Worker(
    ILogger<Worker> logger,
    IConnection connection,
    IConfiguration configuration,
    IServiceProvider serviceProvider)
    : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken ct)
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

        var queueDeclareResult = await _channel.QueueDeclareAsync(
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        string queueName = queueDeclareResult.QueueName;


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

                // Use eventArgs.RoutingKey, not the outer binding variable
                IPreviewGenerationHandler handler = eventArgs.RoutingKey.StartsWith("image.")
                    ? scope.ServiceProvider.GetRequiredService<ImagePreviewGenerationHandler>()
                    : scope.ServiceProvider.GetRequiredService<MediaPreviewGenerationHandler>();

                var message = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
                logger.LogInformation("Received message: {Message} with routing key: {RoutingKey}",
                    message, eventArgs.RoutingKey);

                await handler.HandleAsync(message, ct);
                await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message");
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: false, ct);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        logger.LogInformation("RabbitMQ Worker started and listening on queue: {QueueName}", queueName);

        // Keep running until cancellation
        await Task.Delay(Timeout.Infinite, ct);
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        logger.LogInformation("RabbitMQ Worker stopping...");

        if (_channel != null)
        {
            await _channel.CloseAsync(ct);
            await _channel.DisposeAsync();
        }

        await base.StopAsync(ct);
    }
}