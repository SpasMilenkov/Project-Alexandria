using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public sealed partial class AutoPlaylistSyncService
{
    [LoggerMessage(Level = LogLevel.Debug,
        Message =
            "Synced file {FileId}: {Playlists} playlist(s), {Added} added, {Revived} revived, {Removed} removed.")]
    private static partial void LogFileSynced(
        ILogger logger, Guid fileId, int playlists, int added, int revived, int removed);

    [LoggerMessage(Level = LogLevel.Debug,
        Message =
            "Synced owner {OwnerId}: {Playlists} playlist(s), {Added} added, {Revived} revived, {Removed} removed.")]
    private static partial void LogOwnerSynced(
        ILogger logger, Guid ownerId, int playlists, int added, int revived, int removed);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Dissolved {Count} superseded genre playlist(s) for owner {OwnerId}.")]
    private static partial void LogDissolvedSuperseded(ILogger logger, Guid ownerId, int count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Playlist sync telemetry failed for owner {OwnerId}; the sync result itself is unaffected.")]
    private static partial void LogTelemetryFailed(ILogger logger, Guid ownerId, Exception ex);
}