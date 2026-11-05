using RabbitMQ.Client;

namespace Alexandria.Infrastructure.Messaging;

public interface IChannelPool
{
    ValueTask<IChannel> AcquireChannelAsync(CancellationToken cancellationToken = default);
    void ReleaseChannel(IChannel channel);
}