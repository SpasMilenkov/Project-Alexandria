using System.Text;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Infrastructure.Messaging;
using Alexandria.Workers.Lyrics.Handlers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Workers.Lyrics;

public partial class LyricsWorker : BackgroundService
{
    private IChannel? _channel;
    private readonly SemaphoreSlim _concurrencyGate;
    private readonly ILogger<LyricsWorker> _logger;
    private readonly MessageBrokerConnection _broker;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly DeliveryRetry _deliveryRetry;

    public LyricsWorker(
        ILogger<LyricsWorker> logger,
        MessageBrokerConnection broker,
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        DeliveryRetry deliveryRetry)
    {
        _logger = logger;
        _broker = broker;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _deliveryRetry = deliveryRetry;

        var concurrency = configuration.GetValue("RabbitMQ:Consumer:Concurrency", 2);
        _concurrencyGate = new SemaphoreSlim(concurrency, concurrency);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await ConsumerStartup.RunAsync(_broker, StartConsumingAsync, _logger, ct: ct);
        await Task.Delay(Timeout.Infinite, ct);
    }

    private async Task StartConsumingAsync(IConnection connection, CancellationToken ct)
    {
        _channel = await connection.CreateChannelAsync(cancellationToken: ct);

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
                    await _deliveryRetry.ParkNowAsync(_channel, eventArgs,
                        attempt => ParkAsync(eventArgs, attempt, malformed, parkingRoutingKey, ct), ct);
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<LyricsHandler>();

                await handler.HandleAsync(jobId, ct);

                await DeliverySettlement.AckAsync(_channel, eventArgs.DeliveryTag, _logger, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Not a failed attempt: the retry path would count it towards parking.
                LogStoppingMidMessage(_logger, eventArgs.DeliveryTag);
            }
            catch (Exception ex)
            {
                LogConsumerError(_logger, ex);
                await HandleDeliveryFailureAsync(eventArgs, ex, maxAttempts, retryDelaySeconds,
                    parkingRoutingKey, ct);
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
    }

    // Bounded breather so a persistent outage doesn't spin at full speed; the counted redelivery
    // preserves the attempt history, and poison deliveries end up parked.
    private Task HandleDeliveryFailureAsync(
        BasicDeliverEventArgs eventArgs,
        Exception ex,
        int maxAttempts,
        int retryDelaySeconds,
        string parkingRoutingKey,
        CancellationToken ct)
        => _deliveryRetry.HandleFailureAsync(
            _channel!,
            eventArgs,
            new DeliveryRetryOptions
            {
                MaxAttempts = maxAttempts,
                RetryDelay = TimeSpan.FromSeconds(Math.Max(retryDelaySeconds, 0))
            },
            attempt =>
            {
                LogTriggerParked(_logger, ex, attempt);
                return ParkAsync(eventArgs, attempt, ex, parkingRoutingKey, ct);
            },
            ct);

    // A failed parking publish throws, so the original is requeued and never dropped. Once the
    // copy is parked, the database steps must not throw: a requeued original would be parked a
    // second time on its next failure.
    private async Task ParkAsync(
        BasicDeliverEventArgs eventArgs,
        int attempt,
        Exception ex,
        string parkingRoutingKey,
        CancellationToken ct)
    {
        await _deliveryRetry.RepublishAsync(eventArgs, parkingRoutingKey, attempt, ct);

        try
        {
            await RecordFailureAsync(ex, ct);
        }
        catch (Exception recordEx) when (recordEx is not OperationCanceledException)
        {
            LogParkRecordFailed(_logger, recordEx);
        }

        try
        {
            await MarkParkedJobFailedAsync(eventArgs.Body.ToArray(), ct);
        }
        catch (Exception markEx) when (markEx is not OperationCanceledException)
        {
            LogParkedJobNotMarked(_logger, markEx);
        }
    }

    private async Task MarkParkedJobFailedAsync(byte[] body, CancellationToken ct)
    {
        if (!Guid.TryParse(Encoding.UTF8.GetString(body), out var jobId))
            return;

        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<LyricsHandler>();
        await handler.MarkRetriesExhaustedAsync(jobId, ct);
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

        // Handlers have to be told to stop first: closing the channel waits for the running one.
        await base.StopAsync(ct);

        if (_channel is not null)
            await _channel.CloseAsync(ct);
    }

    // Not in StopAsync: a channel the broker already closed does not wait for the running handler,
    // and that handler still releases the gate when it finishes.
    public override void Dispose()
    {
        _concurrencyGate.Dispose();
        base.Dispose();
    }
}