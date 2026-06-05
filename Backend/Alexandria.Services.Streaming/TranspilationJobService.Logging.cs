using Alexandria.Data.Models.Enumerators;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public partial class TranspilationJobService
{
    [LoggerMessage(5000, LogLevel.Information,
        "Transpilation job {JobId} created for content object {ContentObjectId}")]
    private static partial void LogJobCreated(
        ILogger logger, Guid jobId, Guid contentObjectId);

    [LoggerMessage(5001, LogLevel.Information,
        "Transpilation job {JobId} status updated to {Status}")]
    private static partial void LogJobStatusUpdated(
        ILogger logger, Guid jobId, TranspilationStatus status);

    [LoggerMessage(5002, LogLevel.Debug,
        "Stalled job query returned {Count} job(s) exceeding threshold {Threshold}")]
    private static partial void LogStalledJobsFound(
        ILogger logger, int count, TimeSpan threshold);

    [LoggerMessage(5003, LogLevel.Information,
        "Transpilation job {JobId} successfully claimed for processing")]
    private static partial void LogJobClaimed(ILogger logger, Guid jobId);

    [LoggerMessage(5004, LogLevel.Debug,
        "Transpilation job {JobId} was already claimed by another worker — skipping")]
    private static partial void LogJobClaimSkipped(ILogger logger, Guid jobId);

    [LoggerMessage(5005, LogLevel.Information,
        "Transpilation job {JobId} requeued")]
    private static partial void LogJobRequeued(
        ILogger logger, Guid jobId);

    [LoggerMessage(5006, LogLevel.Information,
        "Transpilation job {JobId} cancellation requested")]
    private static partial void LogJobCancellationRequested(
        ILogger logger, Guid jobId);
}