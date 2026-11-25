using Microsoft.Extensions.Logging;

namespace Alexandria.Infrastructure.Messaging;

public sealed partial class DeliveryRetry
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Could not republish failed delivery for {RoutingKey} (attempt {Attempt}); requeued the original.")]
    private static partial void LogRetryFailed(ILogger logger, Exception ex, string routingKey, int attempt);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Could not park delivery for {RoutingKey} after {Attempt} attempts; requeued the original.")]
    private static partial void LogParkFailed(ILogger logger, Exception ex, string routingKey, int attempt);
}
