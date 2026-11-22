using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Notes;

public sealed partial class FileDraftCheckpointWorker
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Turned {Count} idle drafts into versions.")]
    private static partial void LogCheckpointed(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "The draft of file {FileId} changed after the sweep read it; leaving it to its editor.")]
    private static partial void LogDraftMovedOn(ILogger logger, Guid fileId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Turning the idle draft of file {FileId} into a version failed; the next sweep tries again.")]
    private static partial void LogCheckpointFailed(ILogger logger, Exception ex, Guid fileId);

    [LoggerMessage(Level = LogLevel.Error, Message = "The idle draft sweep failed.")]
    private static partial void LogSweepFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "The idle draft sweep stopped.")]
    private static partial void LogStopped(ILogger logger);
}
