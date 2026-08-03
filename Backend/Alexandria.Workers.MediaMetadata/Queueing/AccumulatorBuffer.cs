using System.Collections.Concurrent;
using System.Threading.Channels;
using Alexandria.Workers.MediaMetadata.Services;

namespace Alexandria.Workers.MediaMetadata.Queueing;

public enum EnqueueResult
{
    /// <summary>The staged file was queued for dispatch.</summary>
    Added,

    /// <summary>The file is already pending dispatch (duplicate trigger).</summary>
    Duplicate,

    /// <summary>The buffer is full; the caller should retry later.</summary>
    BufferFull,
}

/// <summary>
/// In-memory hand-off between the trigger consumer and the batch accumulator.
/// Deduplicates fileIds that are already pending dispatch, so repeated triggers
/// for the same file do not produce duplicate batch entries.
/// </summary>
public sealed class AccumulatorBuffer
{
    private readonly Channel<StagedFile> _channel = Channel.CreateBounded<StagedFile>(
        new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly ConcurrentDictionary<Guid, byte> _pending = new();

    public ChannelReader<StagedFile> Reader => _channel.Reader;

    /// <summary>
    /// Queues a staged file for dispatch. Returns <see cref="EnqueueResult.Added"/> when
    /// enqueued, <see cref="EnqueueResult.Duplicate"/> when the file is already pending,
    /// or <see cref="EnqueueResult.BufferFull"/> when the buffer is full.
    /// </summary>
    public EnqueueResult TryEnqueue(StagedFile file)
    {
        if (!_pending.TryAdd(file.FileId, 0))
            return EnqueueResult.Duplicate;

        if (_channel.Writer.TryWrite(file))
            return EnqueueResult.Added;

        _pending.TryRemove(file.FileId, out _);
        return EnqueueResult.BufferFull;
    }

    /// <summary>
    /// Marks a file as no longer pending once it has been dispatched by the accumulator.
    /// </summary>
    public void MarkDispatched(Guid fileId) => _pending.TryRemove(fileId, out _);

    public void Complete() => _channel.Writer.Complete();
}