namespace Alexandria.Workers.MediaTranspilation.Handlers;

public partial class TranspilationJobHandler
{
    [LoggerMessage(10000, LogLevel.Information,
        "Job {JobId}: downloading source from content object {ContentObjectId}")]
    private static partial void LogDownloadingSource(
        ILogger logger, Guid jobId, Guid contentObjectId);

    [LoggerMessage(10001, LogLevel.Information,
        "Job {JobId}: running {MediaType} transpilation")]
    private static partial void LogTranspilationRunning(
        ILogger logger, Guid jobId, bool mediaType);

    [LoggerMessage(10002, LogLevel.Information,
        "Job {JobId}: uploading output to segment prefix '{SegmentPrefix}'")]
    private static partial void LogUploadingOutput(
        ILogger logger, Guid jobId, string segmentPrefix);

    [LoggerMessage(10003, LogLevel.Information,
        "Job {JobId}: completed successfully")]
    private static partial void LogJobCompleted(ILogger logger, Guid jobId);

    [LoggerMessage(10004, LogLevel.Error,
        "Job {JobId}: failed")]
    private static partial void LogJobFailed(
        ILogger logger, Exception ex, Guid jobId);

    [LoggerMessage(10005, LogLevel.Warning,
        "Job {JobId}: could not mark representation {RepresentationId} as failed during error cleanup")]
    private static partial void LogRepresentationMarkFailedError(
        ILogger logger, Exception ex, Guid JobId, Guid representationId);

    [LoggerMessage(10006, LogLevel.Warning,
        "Job {JobId}: could not update job status to Failed during error cleanup")]
    private static partial void LogJobMarkFailedError(
        ILogger logger, Exception ex, Guid jobId);

    [LoggerMessage(10007, LogLevel.Debug,
        "Job {JobId}: local output directory '{JobDir}' deleted")]
    private static partial void LogLocalOutputCleaned(
        ILogger logger, Guid jobId, string jobDir);

    [LoggerMessage(10008, LogLevel.Warning,
        "Job {JobId}: failed to delete local output directory '{JobDir}'")]
    private static partial void LogLocalOutputCleanupFailed(
        ILogger logger, Exception ex, Guid jobId, string jobDir);
}