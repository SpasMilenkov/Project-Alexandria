using System.Threading.Channels;
using Alexandria.Common.Queues;

namespace Alexandria.Services.Storage.Promotions;

public class PromotionQueueService : IPromotionQueue
{
    private readonly Channel<(Guid, string)> _channel;

    public PromotionQueueService()
    {
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