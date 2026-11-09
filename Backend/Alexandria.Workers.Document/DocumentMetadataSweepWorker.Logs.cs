namespace Alexandria.Workers.Document;

public partial class DocumentMetadataSweepWorker
{
    [LoggerMessage(4280, LogLevel.Information, "Document metadata sweep is disabled")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(4281, LogLevel.Error, "Document metadata sweep failed")]
    private static partial void LogSweepFailed(ILogger logger, Exception ex);

    [LoggerMessage(4282, LogLevel.Warning, "Could not extract document metadata for version {VersionId}")]
    private static partial void LogVersionFailed(ILogger logger, Exception ex, Guid versionId);

    [LoggerMessage(4283, LogLevel.Information, "Document metadata sweep stored {Stored} of {Candidates} version(s)")]
    private static partial void LogBatchCompleted(ILogger logger, int stored, int candidates);

    [LoggerMessage(4284, LogLevel.Information, "Document metadata sweep is retrying {Count} failed version(s)")]
    private static partial void LogRetryingFailures(ILogger logger, int count);
}
