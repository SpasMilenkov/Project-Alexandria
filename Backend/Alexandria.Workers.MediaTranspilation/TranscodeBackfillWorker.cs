using System.Text;
using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Policies;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Workers.MediaTranspilation;

/// <summary>
/// Drains rule-scoped transcode backfill requests. Malformed bodies are discarded; failures retry a
/// bounded number of times and are then recorded as an incident. The sweep only queues jobs, so it
/// runs beside the job consumer instead of taking one of its slots.
/// </summary>
public partial class TranscodeBackfillWorker(
    ILogger<TranscodeBackfillWorker> logger,
    MessageBrokerConnection broker,
    IServiceProvider serviceProvider,
    DeliveryRetry deliveryRetry) : BackgroundService
{
    private const string ExchangeName = "content-exchange";
    private const string QueueName = "transcode-backfill-queue";

    private static readonly JsonSerializerOptions BackfillJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly DeliveryRetryOptions RetryOptions = new()
    {
        MaxAttempts = 5,
        RetryDelay = TimeSpan.FromSeconds(5),
    };

    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConsumerStartup.RunAsync(broker, StartConsumingAsync, logger, ct: stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task StartConsumingAsync(IConnection connection, CancellationToken stoppingToken)
    {
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(0, 1, false, stoppingToken);

        await _channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: $"{TranscodeBackfill.BackfillRoutingKey}.#",
            arguments: null,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, eventArgs) => HandleDeliveryAsync(_channel, eventArgs, stoppingToken);

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        LogConsumerStarted(logger, QueueName, ExchangeName);
    }

    private async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs eventArgs, CancellationToken ct)
    {
        var message = Encoding.UTF8.GetString(eventArgs.Body.Span);
        var request = TryParse(message);

        if (request is null)
        {
            LogMalformedMessage(logger, message);
            await channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: false, ct);
            return;
        }

        try
        {
            using var scope = serviceProvider.CreateScope();
            var backfill = scope.ServiceProvider.GetRequiredService<ITranscodeBackfillService>();

            await backfill.BackfillRuleAsync(request, ct);
            await DeliverySettlement.AckAsync(channel, eventArgs.DeliveryTag, logger, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            LogStoppingMidMessage(logger, eventArgs.DeliveryTag);
        }
        catch (Exception ex)
        {
            // Redelivery is safe: files the earlier attempt already queued are skipped as running
            LogBackfillFailed(logger, ex, request.PolicyId, request.RuleId);
            await deliveryRetry.HandleFailureAsync(channel, eventArgs, RetryOptions, _ => RecordAsync(ex, ct), ct);
        }
    }

    private TranscodeBackfillRequest? TryParse(string message)
    {
        TranscodeBackfillRequest? request;

        try
        {
            request = JsonSerializer.Deserialize<TranscodeBackfillRequest>(message, BackfillJsonOptions);
        }
        catch (JsonException ex)
        {
            LogUnreadableBody(logger, ex);
            return null;
        }

        if (request is null || request.PolicyId == Guid.Empty || request.RuleId == Guid.Empty
            || request.Cutoff == default)
            return null;

        return request;
    }

    private async Task RecordAsync(Exception ex, CancellationToken ct)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await WorkerCycleFailureRecorder.RecordAsync(
                unitOfWork, ServiceType.Transpilation, "transcode-backfill", ex, ct);
        }
        catch (Exception recordEx) when (recordEx is not OperationCanceledException)
        {
            // Recording is best effort; dropping the request must not depend on the database
            LogIncidentRecordFailed(logger, recordEx);
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
