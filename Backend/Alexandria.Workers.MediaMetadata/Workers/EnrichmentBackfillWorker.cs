using System.Text;
using Alexandria.Common.Policies;
using Alexandria.Common.Services;
using Alexandria.Workers.MediaMetadata.Config;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Alexandria.Workers.MediaMetadata.Workers;

/// <summary>
/// Drains backfill requests published on policy create/update (one policy id per
/// message) and runs the enrichment backfill for that policy's scope. Each file goes
/// out over the normal enrich trigger path, so completion reuses the existing
/// results-consumer flow. Registered only while <c>Features:Autotagging</c> is
/// enabled; while disabled, requests wait durably in the queue instead of being
/// dropped. Malformed bodies are discarded; transient failures requeue.
/// </summary>
public partial class EnrichmentBackfillWorker(
    ILogger<EnrichmentBackfillWorker> logger,
    IConnection connection,
    IServiceProvider serviceProvider,
    IOptions<RabbitMqConsumerConfig> rabbitOptions) : BackgroundService
{
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

            if (!Guid.TryParse(message, out var policyId))
            {
                LogMalformedMessage(logger, message);
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: false, stoppingToken);
                return;
            }

            try
            {
                using var scope = serviceProvider.CreateScope();
                var backfill = scope.ServiceProvider.GetRequiredService<IEnrichmentBackfillService>();

                var result = await backfill.BackfillPolicyAsync(policyId, stoppingToken);
                LogBackfillDrained(logger, policyId, result.Published, result.Candidates);
                await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
            }
            catch (Exception ex)
            {
                // Backfill is idempotent per file, so redelivery only re-publishes
                // triggers the shared guard would have skipped anyway.
                LogBackfillError(logger, ex, policyId);
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: true, stoppingToken);
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