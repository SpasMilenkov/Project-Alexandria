namespace Alexandria.Common.Exceptions;

public class MessageBrokerUnavailableException(bool disabled)
    : Exception(disabled
        ? "Background processing is disabled on this server (no message broker configured)."
        : "Background processing is temporarily unavailable: the message broker is not connected.")
{
    public bool Disabled { get; } = disabled;

    public static void ThrowIfUnavailable(Services.IMessageBrokerStatus broker)
    {
        if (!broker.IsEnabled)
            throw new MessageBrokerUnavailableException(disabled: true);

        if (!broker.IsAvailable)
            throw new MessageBrokerUnavailableException(disabled: false);
    }
}
