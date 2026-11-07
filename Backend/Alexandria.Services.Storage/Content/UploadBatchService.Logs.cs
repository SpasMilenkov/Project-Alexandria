using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Content;

public sealed partial class UploadBatchService
{
    [LoggerMessage(LogLevel.Information,
        "Finalized upload batch: {Count} items, {Failed} failed, verify {VerifyElapsed}, commit {CommitElapsed}")]
    private static partial void LogBatchFinalized(
        ILogger logger, int count, int failed, TimeSpan verifyElapsed, TimeSpan commitElapsed);

    [LoggerMessage(LogLevel.Warning, "Upload {UploadId} failed in a finalize batch")]
    private static partial void LogItemFailed(ILogger logger, Exception ex, Guid uploadId);

    [LoggerMessage(LogLevel.Error, "Policy dispatch failed for saved file {FileId}")]
    private static partial void LogDispatchFailed(ILogger logger, Exception ex, Guid fileId);
}
