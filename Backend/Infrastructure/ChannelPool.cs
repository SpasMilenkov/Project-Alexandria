using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace Infrastructure;

public class ChannelPool(IConnection connection, int maxSize = 10) : IChannelPool, IDisposable
{
    private readonly ConcurrentBag<IChannel> _channels = new();
    private readonly SemaphoreSlim _semaphore = new(maxSize, maxSize);
    private bool _disposed;

    public async ValueTask<IChannel> AcquireChannelAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        if (_channels.TryTake(out var channel))
        {
            if (channel.IsOpen)
                return channel;
            
            // Channel is closed, dispose and create new one
            channel.Dispose();
        }

        // Create new channel if under max size
        return await connection.CreateChannelAsync(cancellationToken: cancellationToken);
    }

    public void ReleaseChannel(IChannel channel)
    {
        if (_disposed || !channel.IsOpen)
        {
            channel?.Dispose();
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
            channel?.Dispose();
        }

        _semaphore?.Dispose();
    }
}