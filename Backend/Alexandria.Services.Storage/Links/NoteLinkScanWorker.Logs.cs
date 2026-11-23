using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Links;

public sealed partial class NoteLinkScanWorker
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Indexed the links of {Count} notes.")]
    private static partial void LogSwept(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Indexing the links of version {VersionId} failed; the next sweep tries again.")]
    private static partial void LogNoteFailed(ILogger logger, Exception ex, Guid versionId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "{Count} notes in a row could not be indexed; stopping this batch until the next sweep.")]
    private static partial void LogBatchStopped(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "The note link sweep failed; it tries again shortly.")]
    private static partial void LogSweepFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "The note link sweep stopped.")]
    private static partial void LogStopped(ILogger logger);
}
