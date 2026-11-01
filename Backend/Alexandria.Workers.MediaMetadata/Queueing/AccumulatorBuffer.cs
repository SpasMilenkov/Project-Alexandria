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

    // Broker acknowledgement for each pending trigger, settled once its batch is persisted
    // so a crash before then leaves the trigger unacked and redelivered.
    private readonly ConcurrentDictionary<Guid, Func<bool, Task>> _settlers = new();

    public ChannelReader<StagedFile> Reader => _channel.Reader;

    /// <summary>
    /// Queues a staged file for dispatch. Returns <see cref="EnqueueResult.Added"/> when
    /// enqueued, <see cref="EnqueueResult.Duplicate"/> when the file is already pending,
    /// or <see cref="EnqueueResult.BufferFull"/> when the buffer is full.
    /// </summary>
    /// <remarks>
    /// <paramref name="settle"/> is invoked by the accumulator with <see langword="true"/> once the
    /// file's batch is persisted, or <see langword="false"/> when persisting failed and the trigger
    /// should be redelivered. It is not retained for duplicate or rejected triggers.
    /// </remarks>
    public EnqueueResult TryEnqueue(StagedFile file, Func<bool, Task>? settle = null)
    {
        if (!_pending.TryAdd(file.FileId, 0))
            return EnqueueResult.Duplicate;

        if (settle is not null)
            _settlers[file.FileId] = settle;

        if (_channel.Writer.TryWrite(file))
            return EnqueueResult.Added;

        _settlers.TryRemove(file.FileId, out _);
        _pending.TryRemove(file.FileId, out _);
        return EnqueueResult.BufferFull;
    }

    /// <summary>
    /// Marks a file as no longer pending, whether its batch was dispatched or failed to persist,
    /// so later triggers for it are accepted again.
    /// </summary>
    public void MarkDispatched(Guid fileId) => _pending.TryRemove(fileId, out _);

    /// <summary>
    /// Removes and returns the acknowledgement registered for a pending file, if any.
    /// </summary>
    public Func<bool, Task>? TakeSettler(Guid fileId)
        => _settlers.TryRemove(fileId, out var settle) ? settle : null;

    public void Complete() => _channel.Writer.Complete();
}