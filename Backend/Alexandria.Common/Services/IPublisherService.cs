namespace Alexandria.Common.Services;

/// <summary>
/// Sends straight to the broker and returns once the broker has confirmed the message. Only the
/// outbox relay and consumer-side redelivery use it; everything else enqueues through
/// <see cref="IOutbox"/> so the message is saved with the change it announces.
/// </summary>
public interface IPublisherService
{
    /// <remarks>
    /// Throws RabbitMQ's <c>PublishException</c> when the broker refuses the message or cannot
    /// route it to any queue, and <see cref="Exceptions.MessageBrokerUnavailableException"/> when
    /// there is no connection.
    /// </remarks>
    Task PublishAsync(OutgoingMessage message, CancellationToken ct);
}
