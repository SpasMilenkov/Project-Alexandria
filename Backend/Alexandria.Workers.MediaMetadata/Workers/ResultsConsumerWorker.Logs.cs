namespace Alexandria.Workers.MediaMetadata.Workers;

public partial class ResultsConsumerWorker
{
    [LoggerMessage(22400, LogLevel.Warning, "Malformed completion message (not a valid payload): {Body}")]
    private static partial void LogMalformedMessage(ILogger logger, string body);

    [LoggerMessage(22401, LogLevel.Warning, "Completion for unknown batch {BatchId}; ignoring")]
    private static partial void LogBatchNotFound(ILogger logger, Guid batchId);

    [LoggerMessage(22402, LogLevel.Debug, "Duplicate completion for already-completed batch {BatchId}; ignoring")]
    private static partial void LogBatchAlreadyCompleted(ILogger logger, Guid batchId);

    [LoggerMessage(22403, LogLevel.Warning,
        "No output JSON for file {FileId} in batch {BatchId}; marking MissingOutput")]
    private static partial void LogMissingOutput(ILogger logger, Guid fileId, Guid batchId);

    [LoggerMessage(22404, LogLevel.Warning,
        "Malformed per-file JSON for file {FileId} in batch {BatchId}; marking MissingOutput")]
    private static partial void LogMalformedPerFile(ILogger logger, Guid fileId, Guid batchId);

    [LoggerMessage(22405, LogLevel.Debug, "File {FileId} in batch {BatchId} succeeded")]
    private static partial void LogFileSucceeded(ILogger logger, Guid fileId, Guid batchId);

    [LoggerMessage(22406, LogLevel.Warning, "File {FileId} in batch {BatchId} failed: {Error}")]
    private static partial void LogFileFailed(ILogger logger, Guid fileId, Guid batchId, string error);

    [LoggerMessage(22407, LogLevel.Information, "Batch {BatchId} completed with {Count} files")]
    private static partial void LogBatchCompleted(ILogger logger, Guid batchId, int count);

    [LoggerMessage(22408, LogLevel.Warning, "Failed to clean up {Path}")]
    private static partial void LogCleanupError(ILogger logger, Exception ex, string path);

    [LoggerMessage(22409, LogLevel.Error, "Error processing completion message; requeueing")]
    private static partial void LogProcessError(ILogger logger, Exception ex);

    [LoggerMessage(22410, LogLevel.Information,
        "Results consumer started, listening on queue {QueueName} via exchange {ExchangeName}")]
    private static partial void LogConsumerStarted(ILogger logger, string queueName, string exchangeName);

    [LoggerMessage(22411, LogLevel.Information, "Results consumer stopping")]
    private static partial void LogWorkerStopping(ILogger logger);
}