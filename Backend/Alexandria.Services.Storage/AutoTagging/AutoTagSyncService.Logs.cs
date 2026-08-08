using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.AutoTagging;

public partial class AutoTagSyncService
{
    [LoggerMessage(2201, LogLevel.Debug, "Auto-tag sync: file {FileId} skipped ({Reason})")]
    private static partial void LogFileSkipped(ILogger logger, Guid fileId, string reason);

    [LoggerMessage(2202, LogLevel.Information,
        "Auto-tagged file {FileId} with {Count} candidate(s) for owner {OwnerId}")]
    private static partial void LogAutoTagsSynced(ILogger logger, Guid fileId, int count, Guid ownerId);
}