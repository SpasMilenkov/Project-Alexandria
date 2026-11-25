using Microsoft.Extensions.Logging;

namespace Alexandria.Infrastructure.Messaging.Outbox;

public sealed partial class OutboxWriter
{
    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Dropped outbox message for {RoutingKey}: the message broker is turned off on this server.")]
    private static partial void LogDroppedBrokerDisabled(ILogger logger, string routingKey);
}
