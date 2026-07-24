using System.Text;
using Alexandria.Common.Exceptions.Streaming.Lyrics;
using Alexandria.Workers.MediaMetadata.Handlers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Workers.MediaMetadata;

public partial class LyricsWorker : BackgroundService
{
    private IChannel? _channel;
    private readonly SemaphoreSlim _concurrencyGate;
    private readonly ILogger<LyricsWorker> _logger;
    private readonly IConnection _connection;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public LyricsWorker(
        ILogger<LyricsWorker> logger,
        IConnection connection,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _connection = connection;
        _configuration = configuration;
        _serviceProvider = serviceProvider;

        var concurrency = configuration.GetValue("RabbitMQ:Consumer:Concurrency", 2);
        _concurrencyGate = new SemaphoreSlim(concurrency, concurrency);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        var exchangeName = "content-exchange";
        var routingKey = "lyrics.#";
        var prefetchCount = _configuration.GetValue<ushort>("RabbitMQ:Consumer:PrefetchCount", 2);

        await _channel.BasicQosAsync(0, prefetchCount, false, ct);

        await _channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        var queueName = _configuration.GetValue<string>("RabbitMQ:Consumer:QueueName", "lyrics-queue")!;
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
                var lyricsId = Guid.Parse(Encoding.UTF8.GetString(eventArgs.Body.Span));

                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<LyricsHandler>();

                await handler.HandleAsync(lyricsId, ct);

                await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, ct);
            }
            catch (LyricsNotFoundException ex)
            {
                LogConsumerError(_logger, ex);
                await _channel.BasicRejectAsync(eventArgs.DeliveryTag, false, ct);
            }
            catch (Exception ex)
            {
                LogConsumerError(_logger, ex);
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
        LogWorkerStopping(_logger);
        _concurrencyGate.Dispose();
        if (_channel is not null)
            await _channel.CloseAsync(ct);
        await base.StopAsync(ct);
    }
}