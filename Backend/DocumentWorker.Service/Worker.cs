using System.Text;
using DocumentWorker.Service.Handlers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DocumentWorker.Service;

public class Worker(
    ILogger<Worker> logger,
    IConnection connection,
    IConfiguration configuration,
    IServiceProvider serviceProvider)
    : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RabbitMQ Worker starting...");
    
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        var exchangeName = "document-exchange";
        var routingKey = "document.#";
        var prefetchCount = configuration.GetValue<ushort>("RabbitMQ:Consumer:PrefetchCount", 10);
    
        await _channel.BasicQosAsync(0, prefetchCount, false, stoppingToken);
    
        await _channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        var queueDeclareResult = await _channel.QueueDeclareAsync(
            // queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);
        
        string queueName = queueDeclareResult.QueueName;

        
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
                
                // Process message
                await messageHandler.HandleAsync(message);
                
                // Acknowledge message
                await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message");
                
                // Reject and requeue (or send to DLQ)
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

        // Keep running until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("RabbitMQ Worker stopping...");
        
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }
        
        await base.StopAsync(cancellationToken);
    }
}