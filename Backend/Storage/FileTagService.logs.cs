using Microsoft.Extensions.Logging;

namespace Storage;

public partial class FileTagService
{
    // Tag creation

    [LoggerMessage(2001, LogLevel.Warning, "Tag {TagName} already exists for user {UserId}")]
    private static partial void LogTagAlreadyExists(ILogger logger, string tagName, Guid userId);

    [LoggerMessage(2002, LogLevel.Information, "Tag {TagId} created successfully by user {UserId}")]
    private static partial void LogTagCreated(ILogger logger, Guid tagId, Guid userId);

    [LoggerMessage(2003, LogLevel.Error, "Error creating tag {TagName} for user {UserId}")]
    private static partial void LogTagCreationFailed(ILogger logger, Exception ex, string tagName, Guid userId);

    // Tag update

    [LoggerMessage(2004, LogLevel.Information, "Tag {TagId} updated successfully")]
    private static partial void LogTagUpdated(ILogger logger, Guid tagId);

    [LoggerMessage(2005, LogLevel.Error, "Error updating tag {TagId}")]
    private static partial void LogTagUpdateFailed(ILogger logger, Exception ex, Guid tagId);

    // Tag deletion

    [LoggerMessage(2006, LogLevel.Information, "Tag {TagId} soft-deleted successfully")]
    private static partial void LogTagDeleted(ILogger logger, Guid tagId);

    [LoggerMessage(2007, LogLevel.Error, "Error deleting tag {TagId}")]
    private static partial void LogTagDeletionFailed(ILogger logger, Exception ex, Guid tagId);

    // Add tags to file

    [LoggerMessage(2008, LogLevel.Warning, "Tag {TagId} not found, skipping")]
    private static partial void LogTagNotFoundSkipping(ILogger logger, Guid tagId);

    [LoggerMessage(2009, LogLevel.Warning, "User {UserId} does not own tag {TagId}, skipping")]
    private static partial void LogTagOwnershipMismatchSkipping(ILogger logger, Guid userId, Guid tagId);

    [LoggerMessage(2010, LogLevel.Debug, "File {FileId} already has tag {TagId}, skipping")]
    private static partial void LogFileAlreadyHasTagSkipping(ILogger logger, Guid fileId, Guid tagId);

    [LoggerMessage(2011, LogLevel.Information, "Added {Count} tags to file {FileId}")]
    private static partial void LogTagsAddedToFile(ILogger logger, int count, Guid fileId);

    [LoggerMessage(2012, LogLevel.Error, "Error adding tags to file {FileId}")]
    private static partial void LogAddTagsToFileFailed(ILogger logger, Exception ex, Guid fileId);

    // Remove tag from file

    [LoggerMessage(2013, LogLevel.Warning, "Tag {TagId} not associated with file {FileId}")]
    private static partial void LogTagNotAssociatedWithFile(ILogger logger, Guid tagId, Guid fileId);

    [LoggerMessage(2014, LogLevel.Information, "Removed tag {TagId} from file {FileId}")]
    private static partial void LogTagRemovedFromFile(ILogger logger, Guid tagId, Guid fileId);

    [LoggerMessage(2015, LogLevel.Error, "Error removing tag {TagId} from file {FileId}")]
    private static partial void LogRemoveTagFromFileFailed(ILogger logger, Exception ex, Guid tagId, Guid fileId);

    // Find files by tags

    [LoggerMessage(2016, LogLevel.Error, "Error finding files by tags")]
    private static partial void LogFindFilesByTagsFailed(ILogger logger, Exception ex);

    // Find tags

    [LoggerMessage(2017, LogLevel.Error, "Error finding tags with criteria")]
    private static partial void LogFindTagsFailed(ILogger logger, Exception ex);
}
