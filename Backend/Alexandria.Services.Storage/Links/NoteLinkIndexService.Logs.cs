using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Links;

public sealed partial class NoteLinkIndexService
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Indexed {Count} link targets in content {ContentObjectId}.")]
    private static partial void LogIndexed(ILogger logger, Guid contentObjectId, int count);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Content {ContentObjectId} was indexed by another writer at the same time; keeping theirs.")]
    private static partial void LogIndexedElsewhere(ILogger logger, Guid contentObjectId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Indexing the links of version {VersionId} failed; the link sweep reads it later.")]
    private static partial void LogIndexFailed(ILogger logger, Exception ex, Guid versionId);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Version {VersionId} can no longer be read as a note; skipping it.")]
    private static partial void LogCandidateMovedOn(ILogger logger, Guid versionId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "The stored text of content {ContentObjectId} is missing; recording it as having no links.")]
    private static partial void LogContentMissing(ILogger logger, Exception ex, Guid contentObjectId);
}
