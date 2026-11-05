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

    public bool IsEnabled { get; } = isEnabled;

    public bool IsAvailable => _connection is { IsOpen: true };

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

    public async ValueTask DisposeAsync()
    {
        if (_connection is null)
            return;

        await _connection.DisposeAsync();
    }
}
