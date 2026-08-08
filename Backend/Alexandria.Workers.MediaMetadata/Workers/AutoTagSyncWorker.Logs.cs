namespace Alexandria.Workers.MediaMetadata.Workers;

public partial class AutoTagSyncWorker
{
    [LoggerMessage(22500, LogLevel.Information, "Auto-tag sync worker started")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(22501, LogLevel.Error, "Error auto-tagging file {FileId}")]
    private static partial void LogSyncError(ILogger logger, Exception ex, Guid fileId);

    [LoggerMessage(22502, LogLevel.Information, "Auto-tag sync worker stopped")]
    private static partial void LogStopped(ILogger logger);
}