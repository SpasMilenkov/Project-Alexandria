using RabbitMQ.Client;

namespace Infrastructure;

public interface IChannelPool
{
    ValueTask<IChannel> AcquireChannelAsync(CancellationToken cancellationToken = default);
    void ReleaseChannel(IChannel channel);
}
