using System.Threading.Channels;
using Alexandria.Common.Services;

namespace Alexandria.Infrastructure.Messaging.Outbox;

public sealed class OutboxSignal : IOutboxSignal
{
    // Capacity one: any number of commits before the relay wakes collapse into a single wake-up.
    private readonly Channel<bool> _wakeups = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite
    });

    public void Notify() => _wakeups.Writer.TryWrite(true);

    /// <returns>True when a commit woke the relay, false when <paramref name="timeout"/> elapsed first.</returns>
    public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);

        try
        {
            await _wakeups.Reader.ReadAsync(deadline.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
