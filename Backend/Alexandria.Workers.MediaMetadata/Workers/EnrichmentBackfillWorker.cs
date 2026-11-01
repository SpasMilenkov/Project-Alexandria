using System.Text;
using System.Text.Json;
using Alexandria.Common.Policies;
using Alexandria.Common.Services;
using Alexandria.Dto.Enrichment;
using Alexandria.Workers.MediaMetadata.Config;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Workers.MediaMetadata.Workers;

/// <summary>
/// Drains rule-scoped backfill requests through the normal enrichment path.
/// Registered only while autotagging is enabled. Malformed bodies are discarded;
/// transient failures requeue.
/// </summary>
public partial class EnrichmentBackfillWorker(
    ILogger<EnrichmentBackfillWorker> logger,
    IConnection connection,
    IServiceProvider serviceProvider,
    IOptions<RabbitMqConsumerConfig> rabbitOptions) : BackgroundService
{
    private static readonly JsonSerializerOptions BackfillJsonOptions = new(JsonSerializerDefaults.Web);

    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = rabbitOptions.Value;
        var exchangeName = config.ExchangeName;
        var queueName = config.BackfillQueueName;

        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(0, config.PrefetchCount, false, stoppingToken);

        await _channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

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
            routingKey: $"{EnrichmentBackfill.BackfillRoutingKey}.#",
            arguments: null,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var message = Encoding.UTF8.GetString(eventArgs.Body.ToArray());

            EnrichmentBackfillRequest? request;

            try
            {
                request = JsonSerializer.Deserialize<EnrichmentBackfillRequest>(message, BackfillJsonOptions);
            }
            catch (JsonException)
            {
                request = null;
            }

            if (request is null || request.PolicyId == Guid.Empty || request.RuleId == Guid.Empty
                || request.Cutoff == default)
            {
                LogMalformedMessage(logger, message);
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: false, stoppingToken);
                return;
            }

            try
            {
                using var scope = serviceProvider.CreateScope();
                var backfill = scope.ServiceProvider.GetRequiredService<IEnrichmentBackfillService>();

                var result = await backfill.BackfillRuleAsync(request, stoppingToken);
                LogBackfillDrained(logger, request.PolicyId, result.Published, result.Candidates);
                await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Backfill is idempotent per file, so redelivery only re-publishes
                // triggers the shared guard would have skipped anyway.
                LogBackfillError(logger, ex, request.PolicyId);
                await ConsumerRetry.HandleFailureAsync(_channel, eventArgs, exchangeName, ex, serviceProvider,
                    stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        LogConsumerStarted(logger, queueName, exchangeName);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        LogWorkerStopping(logger);

        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
