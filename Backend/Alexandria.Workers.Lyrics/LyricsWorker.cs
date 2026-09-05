using System.Text;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Workers.Lyrics.Handlers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Workers.Lyrics;

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
        var parkingQueueName =
            _configuration.GetValue<string>("RabbitMQ:Consumer:ParkingQueueName", "lyrics-parking-queue")!;
        var parkingRoutingKey =
            _configuration.GetValue<string>("RabbitMQ:Consumer:ParkingRoutingKey", "lyrics-parking")!;
        var maxAttempts = _configuration.GetValue("RabbitMQ:Consumer:MaxDeliveryAttempts", 5);
        var retryDelaySeconds = _configuration.GetValue("RabbitMQ:Consumer:RetryDelaySeconds", 15);
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

        await _channel.QueueBindAsync(
            queue: queueDeclareResult.QueueName,
            exchange: exchangeName,
            routingKey: routingKey,
            arguments: null,
            cancellationToken: ct);

        // Parking lot for poison deliveries: counted failures land here instead
        // of looping forever, and surface once as a worker-cycle failure event.
        // The parking key must stay outside the lyrics.# binding above.
        await _channel.QueueDeclareAsync(
            queue: parkingQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: parkingQueueName,
            exchange: exchangeName,
            routingKey: parkingRoutingKey,
            arguments: null,
            cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            await _concurrencyGate.WaitAsync(ct);
            try
            {
                var body = eventArgs.Body.ToArray();
                var payload = Encoding.UTF8.GetString(body);

                if (!Guid.TryParse(payload, out var jobId))
                {
                    // Garbage in: can never succeed, park immediately.
                    LogMalformedParked(_logger, payload);
                    var malformed = new FormatException($"Malformed lyrics trigger: '{payload}'");
                    await ParkDeliveryAsync(eventArgs, body, maxAttempts, malformed,
                        parkingRoutingKey, exchangeName, ct);
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<LyricsHandler>();

                await handler.HandleAsync(jobId, ct);

                await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, ct);
            }
            catch (Exception ex)
            {
                LogConsumerError(_logger, ex);
                await HandleDeliveryFailureAsync(eventArgs, ex, maxAttempts, retryDelaySeconds,
                    parkingRoutingKey, exchangeName, ct);
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

    private async Task HandleDeliveryFailureAsync(
        BasicDeliverEventArgs eventArgs,
        Exception ex,
        int maxAttempts,
        int retryDelaySeconds,
        string parkingRoutingKey,
        string exchangeName,
        CancellationToken ct)
    {
        var body = eventArgs.Body.ToArray();
        var attempt = LyricsRetryPolicy.NextAttemptNumber(eventArgs.BasicProperties);

        if (LyricsRetryPolicy.ShouldPark(attempt, maxAttempts))
        {
            LogTriggerParked(_logger, ex, attempt);
            await ParkDeliveryAsync(eventArgs, body, attempt, ex, parkingRoutingKey, exchangeName, ct);
            return;
        }

        // Bounded breather so a persistent outage doesn't spin at full speed;
        // the counted redelivery preserves the attempt history.
        await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), ct);
        await LyricsRetryPolicy.PublishWithAttemptsAsync(
            _channel!, exchangeName, eventArgs.RoutingKey, body, attempt, ct);
        await _channel!.BasicAckAsync(eventArgs.DeliveryTag, false, ct);
    }

    // Parks the delivery and records the failure. If parking itself fails,
    // falls back to requeueing the original so the message is never dropped.
    private async Task ParkDeliveryAsync(
        BasicDeliverEventArgs eventArgs,
        byte[] body,
        int attempt,
        Exception ex,
        string parkingRoutingKey,
        string exchangeName,
        CancellationToken ct)
    {
        try
        {
            await LyricsRetryPolicy.PublishWithAttemptsAsync(
                _channel!, exchangeName, parkingRoutingKey, body, attempt, ct);
            await RecordFailureAsync(ex, ct);
            await _channel!.BasicAckAsync(eventArgs.DeliveryTag, false, ct);
        }
        catch (Exception parkEx)
        {
            LogParkFailed(_logger, parkEx);
            await _channel!.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: true, ct);
        }
    }

    private async Task RecordFailureAsync(Exception ex, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await WorkerCycleFailureRecorder.RecordAsync(
            unitOfWork, ServiceType.Lyrics, "lyrics", ex, ct);
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