using RabbitMQ.Client;

namespace Alexandria.Infrastructure;

public interface IChannelPool
{
    ValueTask<IChannel> AcquireChannelAsync(CancellationToken cancellationToken = default);
    void ReleaseChannel(IChannel channel);
}