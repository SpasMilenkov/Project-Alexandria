using Alexandria.Common.Services;
using RabbitMQ.Client;

namespace Alexandria.Infrastructure.Messaging;

public class PublisherService(IChannelPool channelPool) : IPublisherService
{
    private const string ExchangeName = "content-exchange";

    public async Task PublishAsync(OutgoingMessage message, CancellationToken ct)
    {
        var channel = await channelPool.AcquireChannelAsync(ct);

        try
        {
            await channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: ct);

            // Persistent so queued jobs survive a broker restart; queues are already durable.
            var properties = new BasicProperties
            {
                Persistent = true,
                MessageId = message.MessageId,
                Headers = message.Headers is null ? null : new Dictionary<string, object?>(message.Headers)
            };

            // A message no queue is bound for comes back as an error instead of
            // being dropped by the exchange.
            await channel.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: message.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: message.Body,
                cancellationToken: ct);
        }
        finally
        {
            channelPool.ReleaseChannel(channel);
        }
    }
}
