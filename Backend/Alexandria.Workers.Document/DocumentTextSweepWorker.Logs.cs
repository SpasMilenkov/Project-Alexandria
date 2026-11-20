namespace Alexandria.Workers.Document;

public partial class DocumentTextSweepWorker
{
    [LoggerMessage(4293, LogLevel.Information, "Document text sweep is disabled")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(4294, LogLevel.Error, "Document text sweep cycle failed")]
    private static partial void LogCycleFailed(ILogger logger, Exception ex);

    [LoggerMessage(4295, LogLevel.Warning,
        "Could not download content {ContentObjectId} through version {VersionId} for text extraction")]
    private static partial void LogDownloadFailed(ILogger logger, Exception ex, Guid contentObjectId, Guid versionId);

    [LoggerMessage(4296, LogLevel.Warning, "Could not read the text of content {ContentObjectId}")]
    private static partial void LogExtractionFailed(ILogger logger, Exception ex, Guid contentObjectId);

    [LoggerMessage(4297, LogLevel.Debug,
        "Stored text for content {ContentObjectId}: {Status}, {Characters} characters, truncated {IsTruncated}")]
    private static partial void LogStored(ILogger logger, Guid contentObjectId, string status, int characters,
        bool isTruncated);

    [LoggerMessage(4298, LogLevel.Information, "Document text sweep processed {Processed} of {Candidates} document(s)")]
    private static partial void LogBatchCompleted(ILogger logger, int processed, int candidates);

    [LoggerMessage(4299, LogLevel.Warning, "Could not report the failed document text sweep cycle to monitoring")]
    private static partial void LogCycleFailureNotRecorded(ILogger logger, Exception ex);
}