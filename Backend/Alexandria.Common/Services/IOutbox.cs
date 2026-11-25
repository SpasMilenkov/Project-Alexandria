namespace Alexandria.Common.Services;

/// <summary>
/// The only way application code sends a broker message. The message is written in the caller's
/// unit-of-work transaction, so it is saved exactly when the change it announces is, and a relay
/// delivers it after the commit.
/// </summary>
public interface IOutbox
{
    /// <summary>
    /// Adds the message to the current transaction. Nothing is written until the caller commits.
    /// On a server with the broker turned off the message is dropped, since nothing would send it.
    /// </summary>
    /// <exception cref="InvalidOperationException">No unit-of-work transaction is open.</exception>
    Task EnqueueAsync(string routingKey, byte[] body, CancellationToken ct);

    /// <summary>
    /// Adds a message that starts <paramref name="jobId"/>. The body is the job id, which is what
    /// workers claim. If the message dies, the job is failed instead of staying Queued.
    /// </summary>
    /// <exception cref="InvalidOperationException">No unit-of-work transaction is open.</exception>
    Task EnqueueJobAsync(string routingKey, Guid jobId, CancellationToken ct);
}
