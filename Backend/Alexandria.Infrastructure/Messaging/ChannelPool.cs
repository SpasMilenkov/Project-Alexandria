using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace Alexandria.Infrastructure.Messaging;

public sealed class ChannelPool(MessageBrokerConnection broker, TimeSpan connectWait, int maxSize = 10)
    : IChannelPool, IDisposable
{
    private readonly ConcurrentBag<IChannel> _channels = new();
    private readonly SemaphoreSlim _semaphore = new(maxSize, maxSize);
    private bool _disposed;

    public async ValueTask<IChannel> AcquireChannelAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            if (_channels.TryTake(out var channel))
            {
                if (channel.IsOpen)
                    return channel;

                channel.Dispose();
            }

            // Resolved per acquire rather than in the constructor, so building the pool never
            // blocks on the broker and a broker that appears later is picked up.
            var connection = await broker.GetConnectionAsync(connectWait, cancellationToken);

            return await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        }
        catch
        {
            // Without this, every failed acquire during an outage leaks a permit until all
            // later publishes block forever.
            _semaphore.Release();
            throw;
        }
    }

    public void ReleaseChannel(IChannel channel)
    {
        if (_disposed || !channel.IsOpen)
        {
            channel.Dispose();
            if (!_disposed)
                _semaphore.Release();
            return;
        }

        _channels.Add(channel);
        _semaphore.Release();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        while (_channels.TryTake(out var channel))
        {
            channel.Dispose();
        }

        _semaphore.Dispose();
    }
}
