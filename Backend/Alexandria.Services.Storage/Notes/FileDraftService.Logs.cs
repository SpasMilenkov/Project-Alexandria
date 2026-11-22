using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Notes;

public sealed partial class FileDraftService
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Discarded the draft of file {FileId} at revision {Revision}.")]
    private static partial void LogDraftDiscarded(ILogger logger, Guid fileId, Guid revision);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Version {VersionId} of file {FileId} was saved, but running the folder's policies on it failed.")]
    private static partial void LogDispatchFailed(ILogger logger, Exception ex, Guid fileId, Guid versionId);
}
