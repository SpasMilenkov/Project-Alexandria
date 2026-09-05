namespace Alexandria.Workers.MediaMetadata.Workers;

public partial class TriggerConsumerWorker
{
    [LoggerMessage(22200, LogLevel.Warning, "Malformed trigger message (not a Guid): {Message}")]
    private static partial void LogMalformedMessage(ILogger logger, string message);

    [LoggerMessage(22201, LogLevel.Warning, "Trigger ignored: file {FileId} is not available for staging")]
    private static partial void LogFileUnavailable(ILogger logger, Guid fileId);

    [LoggerMessage(22202, LogLevel.Information, "Trigger queued for dispatch: file {FileId} staged at {FilePath}")]
    private static partial void LogTriggerQueued(ILogger logger, Guid fileId, string filePath);

    [LoggerMessage(22203, LogLevel.Debug, "Trigger dropped as duplicate: file {FileId} already pending")]
    private static partial void LogDuplicateTrigger(ILogger logger, Guid fileId);

    [LoggerMessage(22204, LogLevel.Warning, "Accumulator buffer full, requeueing trigger for file {FileId}")]
    private static partial void LogBufferFull(ILogger logger, Guid fileId);

    [LoggerMessage(22205, LogLevel.Error, "Failed to stage file {FileId}; requeueing trigger")]
    private static partial void LogStageError(ILogger logger, Exception ex, Guid fileId);

    [LoggerMessage(22208, LogLevel.Error,
        "Staging failed {Attempts} times for file {FileId}; trigger parked, failure event recorded")]
    private static partial void LogTriggerParked(ILogger logger, Exception ex, Guid fileId, int attempts);

    [LoggerMessage(22209, LogLevel.Error, "Failed to park poison trigger for file {FileId}; requeueing")]
    private static partial void LogParkFailed(ILogger logger, Exception ex, Guid fileId);

    [LoggerMessage(22206, LogLevel.Information,
        "Trigger consumer started, listening on queue {QueueName} via exchange {ExchangeName}")]
    private static partial void LogConsumerStarted(ILogger logger, string queueName, string exchangeName);

    [LoggerMessage(22207, LogLevel.Information, "Trigger consumer stopping")]
    private static partial void LogWorkerStopping(ILogger logger);
}