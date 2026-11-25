using Microsoft.Extensions.Logging;

namespace Alexandria.Infrastructure.Messaging.Outbox;

public sealed partial class OutboxDispatcher
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Outbox message {MessageId} for {RoutingKey} could not be sent (attempt {Attempts}); retrying later.")]
    private static partial void LogSendFailed(ILogger logger, Exception ex, Guid messageId, string routingKey, int attempts);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Outbox message {MessageId} for {RoutingKey} could not be sent {Attempts} times and is now dead; resend it from the job recovery panel.")]
    private static partial void LogDead(ILogger logger, Exception ex, Guid messageId, string routingKey, int attempts);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Lost the broker connection while sending outbox message {MessageId}; the rest of the batch waits for the next cycle.")]
    private static partial void LogConnectionLost(ILogger logger, Exception ex, Guid messageId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Could not record the incident for a dead outbox message.")]
    private static partial void LogIncidentFailed(ILogger logger, Exception ex);
}
