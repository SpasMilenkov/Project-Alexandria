using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using RabbitMQ.Client;

namespace Alexandria.Infrastructure.Messaging;

/// <summary>
/// Process-wide holder of the RabbitMQ connection. <see cref="MessageBrokerSupervisor"/>
/// establishes it in the background, so nothing blocks or crashes at startup when the broker
/// is missing. After the first connect, the client's automatic recovery keeps it alive.
/// </summary>
public sealed class MessageBrokerConnection(bool isEnabled) : IMessageBrokerStatus, IAsyncDisposable
{
    private readonly TaskCompletionSource<IConnection> _connected =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private IConnection? _connection;
    private volatile bool _blocked;

    public bool IsEnabled { get; } = isEnabled;

    /// <summary>
    /// False while the connection is down or the broker has blocked it over a resource alarm. A
    /// blocked connection stays open but takes no publishes, so it counts as an outage rather than
    /// a failure of the message being sent.
    /// </summary>
    public bool IsAvailable => _connection is { IsOpen: true } && !_blocked;

    /// <summary>
    /// Completes once the first connection is up. While the broker is disabled it only ends
    /// through cancellation, which keeps consumers idle instead of failing.
    /// </summary>
    public Task<IConnection> WaitForConnectionAsync(CancellationToken ct = default)
        => _connected.Task.WaitAsync(ct);

    /// <summary>
    /// For publishers: waits at most <paramref name="wait"/> for the first connection (covers the
    /// boot race), otherwise fails fast with <see cref="MessageBrokerUnavailableException"/>.
    /// </summary>
    public async Task<IConnection> GetConnectionAsync(TimeSpan wait, CancellationToken ct = default)
    {
        if (!IsEnabled)
            throw new MessageBrokerUnavailableException(disabled: true);

        // Every publish on a blocked connection would wait out a protocol timeout and then fail.
        if (_blocked)
            throw new MessageBrokerUnavailableException(disabled: false);

        if (_connection is { IsOpen: true } open)
            return open;

        // A connection that exists but is closed is mid-recovery; waiting would only stall
        // the caller for an unknown time.
        if (_connection is not null)
            throw new MessageBrokerUnavailableException(disabled: false);

        try
        {
            return await _connected.Task.WaitAsync(wait, ct);
        }
        catch (TimeoutException)
        {
            throw new MessageBrokerUnavailableException(disabled: false);
        }
    }

    internal void SetConnection(IConnection connection)
    {
        _connection = connection;
        _connected.TrySetResult(connection);
    }

    /// <summary>
    /// Follows the broker's blocked and unblocked notifications. A recovered or closed connection
    /// clears the flag, since the unblock for the old socket never arrives.
    /// </summary>
    internal void SetBlocked(bool blocked) => _blocked = blocked;

    public async ValueTask DisposeAsync()
    {
        if (_connection is null)
            return;

        await _connection.DisposeAsync();
    }
}
