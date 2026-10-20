using System.Threading.Channels;

namespace Alexandria.Workers.Playlist.Queueing;

public enum EnqueueResult
{
    /// <summary>The file was added to the pending buffer.</summary>
    Added,

    /// <summary>The file is already pending; its delivery was folded into that entry.</summary>
    Duplicate,

    /// <summary>The buffer is full; the caller should requeue the delivery.</summary>
    BufferFull,
}

/// <summary>
/// In-memory hand-off between the playlist consumer and the batch drainer.
/// Pending files are keyed by owner and file so repeated events for the same
/// file share one sync. Delivery tags accumulate on the pending entry; the
/// worker acknowledges them only after the owner batch has been applied.
/// </summary>
public sealed class PlaylistSyncBuffer
{
    private readonly object _gate = new();
    private readonly Channel<(Guid OwnerId, Guid FileId)> _channel;
    private readonly Dictionary<(Guid OwnerId, Guid FileId), PendingFile> _pending = [];

    public PlaylistSyncBuffer(int capacity = 10_000)
    {
        _channel = Channel.CreateBounded<(Guid OwnerId, Guid FileId)>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });
    }

    public EnqueueResult TryEnqueue(Guid ownerId, Guid fileId, ulong deliveryTag)
    {
        var key = (ownerId, fileId);

        lock (_gate)
        {
            if (_pending.TryGetValue(key, out var existing))
            {
                existing.DeliveryTags.Add(deliveryTag);
                return EnqueueResult.Duplicate;
            }

            var pending = new PendingFile(ownerId, fileId);
            pending.DeliveryTags.Add(deliveryTag);
            _pending[key] = pending;

            if (_channel.Writer.TryWrite(key))
                return EnqueueResult.Added;

            _pending.Remove(key);
            return EnqueueResult.BufferFull;
        }
    }

    /// <summary>
    /// Reads up to <paramref name="maxSize"/> distinct pending files, flushing a partial
    /// batch after <paramref name="maxWait"/>. Pending entries are removed before the
    /// batch is returned, so a new event for the same file starts a new batch while this
    /// one is being applied.
    /// </summary>
    public async Task<IReadOnlyList<PlaylistSyncWorkItem>> ReadBatchAsync(
        int maxSize,
        TimeSpan maxWait,
        CancellationToken ct = default)
    {
        maxSize = Math.Max(maxSize, 1);

        var keys = new List<(Guid OwnerId, Guid FileId)>(maxSize);

        try
        {
            keys.Add(await _channel.Reader.ReadAsync(ct));
        }
        catch (ChannelClosedException)
        {
            return [];
        }

        while (keys.Count < maxSize && _channel.Reader.TryRead(out var key))
            keys.Add(key);

        var flushAt = DateTime.UtcNow + maxWait;

        while (keys.Count < maxSize)
        {
            var remaining = flushAt - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(remaining);

            try
            {
                keys.Add(await _channel.Reader.ReadAsync(timeout.Token));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                break;
            }
            catch (ChannelClosedException)
            {
                break;
            }
        }

        var batch = new List<PlaylistSyncWorkItem>(keys.Count);

        lock (_gate)
        {
            foreach (var key in keys)
            {
                if (!_pending.Remove(key, out var pending))
                    continue;

                batch.Add(pending.ToWorkItem());
            }
        }

        return batch;
    }

    private sealed class PendingFile(Guid ownerId, Guid fileId)
    {
        public Guid OwnerId { get; } = ownerId;

        public Guid FileId { get; } = fileId;

        public List<ulong> DeliveryTags { get; } = [];

        public PlaylistSyncWorkItem ToWorkItem() =>
            new(OwnerId, FileId, DeliveryTags.ToArray());
    }
}

public sealed record PlaylistSyncWorkItem(
    Guid OwnerId,
    Guid FileId,
    IReadOnlyList<ulong> DeliveryTags);