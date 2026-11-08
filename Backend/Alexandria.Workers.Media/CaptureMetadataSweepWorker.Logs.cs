namespace Alexandria.Workers.Media;

public partial class CaptureMetadataSweepWorker
{
    [LoggerMessage(4262, LogLevel.Information, "Capture metadata sweep is disabled")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(4263, LogLevel.Error, "Capture metadata sweep failed")]
    private static partial void LogSweepFailed(ILogger logger, Exception ex);

    [LoggerMessage(4264, LogLevel.Warning, "Could not extract capture metadata for version {VersionId}")]
    private static partial void LogVersionFailed(ILogger logger, Exception ex, Guid versionId);

    [LoggerMessage(4265, LogLevel.Information, "Capture metadata sweep stored {Stored} of {Candidates} version(s)")]
    private static partial void LogBatchCompleted(ILogger logger, int stored, int candidates);

    [LoggerMessage(4266, LogLevel.Information, "Capture metadata sweep is retrying {Count} failed version(s)")]
    private static partial void LogRetryingFailures(ILogger logger, int count);
}
