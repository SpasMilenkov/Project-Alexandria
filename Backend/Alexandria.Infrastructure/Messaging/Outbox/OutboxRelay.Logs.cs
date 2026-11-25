using Microsoft.Extensions.Logging;

namespace Alexandria.Infrastructure.Messaging.Outbox;

public sealed partial class OutboxRelay
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Outbox relay is off: the message broker is turned off on this server.")]
    private static partial void LogBrokerDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Outbox relay cycle failed; retrying after the poll interval.")]
    private static partial void LogCycleFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Pruned {Count} sent outbox messages past retention.")]
    private static partial void LogPruned(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Outbox relay stopping; unsent messages stay in the table for the next start.")]
    private static partial void LogStopping(ILogger logger);
}
