namespace Alexandria.Workers.MediaMetadata.Workers;

public partial class TimeoutSweepWorker
{
    [LoggerMessage(22500, LogLevel.Information, "Timeout sweep started, checking every {Interval}")]
    private static partial void LogSweepStarted(ILogger logger, TimeSpan interval);

    [LoggerMessage(22501, LogLevel.Warning, "Marked {TimedOut}/{Overdue} overdue batches as TimedOut")]
    private static partial void LogBatchesTimedOut(ILogger logger, int timedOut, int overdue);

    [LoggerMessage(22502, LogLevel.Warning, "Batch {BatchId} timed out with {Count} pending files marked Failed")]
    private static partial void LogBatchFilesTimedOut(ILogger logger, Guid batchId, int count);

    [LoggerMessage(22503, LogLevel.Error, "Timeout sweep failed; retrying next interval")]
    private static partial void LogSweepError(ILogger logger, Exception ex);
}