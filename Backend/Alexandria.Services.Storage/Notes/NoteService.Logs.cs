using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Notes;

public sealed partial class NoteService
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Created the Notes folder {DirectoryId} for user {UserId}.")]
    private static partial void LogNotesFolderCreated(ILogger logger, Guid directoryId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Marked the existing root folder {DirectoryId} as the Notes folder for user {UserId}.")]
    private static partial void LogNotesFolderAdopted(ILogger logger, Guid directoryId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Another request created the Notes folder for user {UserId} first; using that one.")]
    private static partial void LogNotesFolderRaced(ILogger logger, Exception ex, Guid userId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Created note {FileId} in folder {DirectoryId} for user {UserId}.")]
    private static partial void LogNoteCreated(ILogger logger, Guid fileId, Guid directoryId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "The note name {FileName} was taken in folder {DirectoryId} meanwhile; trying the next one.")]
    private static partial void LogNoteNameTaken(ILogger logger, Exception ex, string fileName, Guid directoryId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Renamed note {FileId} (from its first line: {FromText}).")]
    private static partial void LogNoteRenamed(ILogger logger, Guid fileId, bool fromText);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Could not name untitled note {FileId} after its first line; the next save tries again.")]
    private static partial void LogUntitledNameFailed(ILogger logger, Exception ex, Guid fileId);
}
