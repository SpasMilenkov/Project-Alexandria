using Alexandria.Common.Services;
using RabbitMQ.Client;

namespace Alexandria.Infrastructure.Messaging;

public class PublisherService(IChannelPool channelPool) : IPublisherService
{
    private const string ExchangeName = "content-exchange";

    public async Task PublishAsync(byte[] body, string routingKey)
    {
        var channel = await channelPool.AcquireChannelAsync();

        try
        {
            await channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null);

            // Persistent so queued jobs survive a broker restart; queues are already durable.
            var properties = new BasicProperties { Persistent = true };

            await channel.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body);
        }
        finally
        {
            channelPool.ReleaseChannel(channel);
        }
    }
}
