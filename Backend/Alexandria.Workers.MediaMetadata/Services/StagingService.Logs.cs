namespace Alexandria.Workers.MediaMetadata.Services;

public partial class StagingService
{
    [LoggerMessage(22100, LogLevel.Warning, "Staging skipped: file {FileId} does not exist")]
    private static partial void LogFileNotFound(ILogger logger, Guid fileId);

    [LoggerMessage(22101, LogLevel.Warning, "Staging skipped: file {FileId} has no current version")]
    private static partial void LogVersionNotFound(ILogger logger, Guid fileId);

    [LoggerMessage(22102, LogLevel.Information, "Staged file {FileId} ({MimeType}) to {StagedPath} ({Size} bytes)")]
    private static partial void LogFileStaged(ILogger logger, Guid fileId, string mimeType, string stagedPath,
        long size);
}