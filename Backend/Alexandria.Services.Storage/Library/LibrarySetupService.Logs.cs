using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Library;

public sealed partial class LibrarySetupService
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "User {UserId} finished the tour with {FolderCount} starter folders.")]
    private static partial void LogTourFinished(ILogger logger, Guid userId, int folderCount);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "User {UserId} tried to finish the tour again; nothing was created.")]
    private static partial void LogTourAlreadyFinished(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Finishing the tour failed for user {UserId}; no folders were created.")]
    private static partial void LogTourFinishFailed(ILogger logger, Exception ex, Guid userId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Starter folder {DirectoryId} for user {UserId} already has automation; it was kept as is.")]
    private static partial void LogExistingPolicyKept(ILogger logger, Guid directoryId, Guid userId);
}
