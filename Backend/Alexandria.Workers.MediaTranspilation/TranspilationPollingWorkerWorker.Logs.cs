namespace Alexandria.Workers.MediaTranspilation;

public partial class TranspilationPollingWorker
{
    [LoggerMessage(11000, LogLevel.Information,
        "Transpilation polling worker starting — polling every {IntervalSeconds}s")]
    private static partial void LogPollingWorkerStarting(ILogger logger, int intervalSeconds);

    [LoggerMessage(11001, LogLevel.Debug,
        "No queued transpilation jobs found")]
    private static partial void LogNoPendingJobs(ILogger logger);

    [LoggerMessage(11002, LogLevel.Information,
        "Found {Count} queued transpilation job(s)")]
    private static partial void LogFoundQueuedJobs(ILogger logger, int count);

    [LoggerMessage(11003, LogLevel.Error,
        "Unhandled error processing job {JobId}")]
    private static partial void LogUnhandledJobError(
        ILogger logger, Exception ex, Guid jobId);

    [LoggerMessage(11004, LogLevel.Error,
        "Transpilation poll cycle failed")]
    private static partial void LogPollCycleError(ILogger logger, Exception ex);

    [LoggerMessage(11005, LogLevel.Information,
        "Transpilation worker stopping")]
    private static partial void LogPollingWorkerStopping(ILogger logger);

    [LoggerMessage(11006, LogLevel.Information, "Found {Count} failed transpilation job(s)")]
    private static partial void LogFoundFailedJobs(ILogger logger, int count);
}