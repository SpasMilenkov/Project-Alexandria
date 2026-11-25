namespace Alexandria.Common.Exceptions;

public class MessageBrokerUnavailableException(bool disabled)
    : Exception(disabled
        ? "Background processing is disabled on this server (no message broker configured)."
        : "Background processing is temporarily unavailable: the message broker is not connected.")
{
    public bool Disabled { get; } = disabled;

    /// <summary>
    /// Refuses work on a server without a broker. A broker that is only down is not refused: the
    /// outbox keeps the message until the broker is back.
    /// </summary>
    public static void ThrowIfDisabled(Services.IMessageBrokerStatus broker)
    {
        if (!broker.IsEnabled)
            throw new MessageBrokerUnavailableException(disabled: true);
    }
}
