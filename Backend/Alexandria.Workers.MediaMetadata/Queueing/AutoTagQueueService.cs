using System.Threading.Channels;

namespace Alexandria.Workers.MediaMetadata.Queueing;

public class AutoTagQueueService : IAutoTagQueue
{
    private readonly Channel<Guid> _channel;

    public AutoTagQueueService()
    {
        _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = true, // Only AutoTagSyncWorker drains
            SingleWriter = false
        });
    }

    public async ValueTask QueueFileAsync(Guid fileId, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(fileId, ct);
    }

    public ChannelReader<Guid> Reader => _channel.Reader;
}