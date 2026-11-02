namespace Alexandria.Workers.MediaMetadata.Workers;

public partial class StagingRecoveryService
{
    [LoggerMessage(22900, LogLevel.Information,
        "Staging recovery removed {Files} orphaned staged files and {Folders} orphaned batch folders")]
    private static partial void LogRecovered(ILogger logger, int files, int folders);

    [LoggerMessage(22901, LogLevel.Warning,
        "Staging recovery could not load dispatched batches; keeping all {Count} batch folders")]
    private static partial void LogBatchLookupFailed(ILogger logger, Exception ex, int count);

    [LoggerMessage(22902, LogLevel.Warning, "Staging recovery could not remove {Path}")]
    private static partial void LogCleanupError(ILogger logger, Exception? ex, string path);

    [LoggerMessage(22903, LogLevel.Error, "Staging recovery failed; continuing startup")]
    private static partial void LogRecoveryFailed(ILogger logger, Exception ex);
}
