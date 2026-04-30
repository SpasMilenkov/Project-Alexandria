using Alexandria.Common.Services;
using RabbitMQ.Client;

namespace Alexandria.Infrastructure;

public class PublisherService(IChannelPool channelPool) : IPublisherService
{
    public async Task PublishAsync(byte[] body, string routingKey)
    {
        var channel = await channelPool.AcquireChannelAsync();
        var exchangeName = "content-exchange";
        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null);

        try
        {
            await channel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: routingKey,
                body: body,
                mandatory: false);
        }
        finally
        {
            channelPool.ReleaseChannel(channel);
        }
    }
}