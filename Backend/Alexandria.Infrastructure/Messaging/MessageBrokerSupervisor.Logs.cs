using Microsoft.Extensions.Logging;

namespace Alexandria.Infrastructure.Messaging;

public sealed partial class MessageBrokerSupervisor
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "RabbitMQ is disabled by configuration (RabbitMQ:Enabled=false); queue consumers and job publishing are off.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "RabbitMQ connected to {Endpoint} on attempt {Attempt}.")]
    private static partial void LogConnected(ILogger logger, string endpoint, int attempt);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "RabbitMQ at {Endpoint} is unavailable ({Reason}); queue features are paused and the connection is retried in the background.")]
    private static partial void LogUnavailable(ILogger logger, string endpoint, string reason);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "RabbitMQ at {Endpoint} is still unavailable after {Attempt} attempts ({Reason}).")]
    private static partial void LogStillUnavailable(ILogger logger, string endpoint, int attempt, string reason);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "RabbitMQ attempt {Attempt} to {Endpoint} failed, retrying in {DelaySeconds}s.")]
    private static partial void LogAttemptFailed(ILogger logger, string endpoint, int attempt, double delaySeconds);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "RabbitMQ connection lost ({Reason}); automatic recovery is in progress.")]
    private static partial void LogConnectionLost(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "RabbitMQ connection recovered.")]
    private static partial void LogRecovered(ILogger logger);
}
