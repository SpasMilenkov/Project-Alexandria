using System.Text;
using Alexandria.Workers.MediaTranspilation.Handlers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Workers.MediaTranspilation;

public partial class TranspilationWorker(
    ILogger<TranspilationWorker> logger,
    IConnection connection,
    IConfiguration configuration,
    IServiceProvider serviceProvider) : BackgroundService
{
    private IChannel? _channel;
    private readonly SemaphoreSlim _concurrencyGate = new(2, 2);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _channel = await connection.CreateChannelAsync(cancellationToken: ct);

        var exchangeName = "content-exchange";
        var routingKey = "transpilation.#";
        var prefetchCount = configuration.GetValue<ushort>("RabbitMQ:Consumer:PrefetchCount", 2);

        await _channel.BasicQosAsync(0, prefetchCount, false, ct);

        await _channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        var queueName = configuration.GetValue<string>("RabbitMQ:Consumer:QueueName", "content-queue")!;

        var queueDeclareResult = await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: queueDeclareResult.QueueName,
            exchange: exchangeName,
            routingKey: routingKey,
            arguments: null,
            cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            await _concurrencyGate.WaitAsync(ct);
            try
            {
                var jobId = Guid.Parse(Encoding.UTF8.GetString(eventArgs.Body.Span));

                using var scope = serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<TranspilationJobHandler>();

                await handler.HandleAsync(jobId, ct);

                await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, ct);
            }
            catch (Exception ex)
            {
                LogConsumerError(logger, ex);
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: true, ct);
            }
            finally
            {
                _concurrencyGate.Release();
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueDeclareResult.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        await Task.Delay(Timeout.Infinite, ct);
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        LogWorkerStopping(logger);
        _concurrencyGate.Dispose();
        if (_channel is not null)
            await _channel.CloseAsync(ct);
        await base.StopAsync(ct);
    }
}