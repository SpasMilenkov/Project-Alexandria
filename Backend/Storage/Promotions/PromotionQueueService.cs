using System.Threading.Channels;
using Common.Queues;

namespace Storage;

public class PromotionQueueService : IPromotionQueue
{
    private readonly Channel<(Guid, string)> _channel;

    public PromotionQueueService()
    {
        // Unbounded channel - if this grows too large, consider bounded with backpressure
        _channel = Channel.CreateUnbounded<(Guid, string)>(new UnboundedChannelOptions
        {
            SingleReader = false, // Multiple workers can read if needed
            SingleWriter = false
        });
    }

    public async ValueTask QueuePromotionAsync(Guid contentObjectId, string tempObjectKey,
        CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync((contentObjectId, tempObjectKey), ct);
    }

    public ChannelReader<(Guid, string)> Reader => _channel.Reader;
}