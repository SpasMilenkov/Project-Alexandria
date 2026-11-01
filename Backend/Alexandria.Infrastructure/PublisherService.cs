using Alexandria.Common.Services;
using RabbitMQ.Client;

namespace Alexandria.Infrastructure;

public class PublisherService(Lazy<Task<IConnection>> connectionLazy, IChannelPool channelPool) : IPublisherService
{
    public async Task PublishAsync(byte[] body, string routingKey)
    {
        await connectionLazy.Value;

        var channel = await channelPool.AcquireChannelAsync();
        var exchangeName = "content-exchange";
        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null);

        // Persistent so queued jobs survive a broker restart; queues are already durable.
        var properties = new BasicProperties { Persistent = true };

        try
        {
            await channel.BasicPublishAsync(
                exchange: exchangeName,
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