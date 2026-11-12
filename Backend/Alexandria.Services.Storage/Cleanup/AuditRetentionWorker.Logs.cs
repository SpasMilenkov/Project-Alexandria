using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Cleanup;

public sealed partial class AuditRetentionWorker
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Purged {Deleted} audit rows older than {Cutoff} ({RetentionDays} day retention)")]
    private static partial void LogPurged(ILogger logger, int deleted, DateTimeOffset cutoff, int retentionDays);

    [LoggerMessage(Level = LogLevel.Error, Message = "Audit retention purge failed")]
    private static partial void LogPurgeFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Audit retention worker stopped")]
    private static partial void LogStopped(ILogger logger);
}
