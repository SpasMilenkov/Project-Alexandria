using System.Text;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Infrastructure.Messaging;
using Alexandria.Workers.MediaTranspilation.Handlers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Workers.MediaTranspilation;

public partial class TranspilationWorker(
    ILogger<TranspilationWorker> logger,
    MessageBrokerConnection broker,
    IConfiguration configuration,
    IServiceProvider serviceProvider,
    DeliveryRetry deliveryRetry) : BackgroundService
{
    private const string UnclaimedError = "delivery kept failing before a worker could claim the job";

    private IChannel? _channel;
    private readonly SemaphoreSlim _concurrencyGate = new(2, 2);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await ConsumerStartup.RunAsync(broker, StartConsumingAsync, logger, ct: ct);
        await Task.Delay(Timeout.Infinite, ct);
    }

    private async Task StartConsumingAsync(IConnection connection, CancellationToken ct)
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

        var queueName = configuration.GetValue<string>("RabbitMQ:Consumer:QueueName", "transpilation-queue")!;
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
                if (!Guid.TryParse(Encoding.UTF8.GetString(eventArgs.Body.Span), out var jobId))
                {
                    // Requeueing an unparseable message would redeliver it forever.
                    LogMalformedMessage(logger);
                    await _channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: false, ct);
                    return;
                }

                using var scope = serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<TranspilationJobHandler>();

                await handler.HandleAsync(jobId, ct);

                await DeliverySettlement.AckAsync(_channel, eventArgs.DeliveryTag, logger, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                LogStoppingMidMessage(logger, eventArgs.DeliveryTag);
            }
            catch (Exception ex)
            {
                // The handler records failures after its claim on the job itself, so what lands
                // here failed before any claim. A plain requeue redelivered it at full speed for
                // as long as the cause lasted; this pauses, counts the attempt and finally parks.
                LogConsumerError(logger, ex);
                await deliveryRetry.HandleFailureAsync(
                    _channel, eventArgs, new DeliveryRetryOptions(), _ => ParkAsync(eventArgs, ex, ct), ct);
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

    // Fails the job only while it is still Queued: a worker that did claim it keeps the outcome
    // it recorded. A failed update throws, so the delivery is kept instead of dropped.
    private async Task ParkAsync(BasicDeliverEventArgs eventArgs, Exception ex, CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        if (Guid.TryParse(Encoding.UTF8.GetString(eventArgs.Body.Span), out var jobId))
            await unitOfWork.Jobs.FailIfQueuedAsync(jobId, UnclaimedError, ct);

        try
        {
            await WorkerCycleFailureRecorder.RecordAsync(
                unitOfWork, ServiceType.Transpilation, "transpilation", ex, ct);
        }
        catch (Exception recordEx) when (recordEx is not OperationCanceledException)
        {
            // The job is already failed and visible; the incident is extra signal.
            LogIncidentRecordFailed(logger, recordEx);
        }
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        LogWorkerStopping(logger);

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