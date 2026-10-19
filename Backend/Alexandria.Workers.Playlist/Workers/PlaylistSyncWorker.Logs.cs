namespace Alexandria.Workers.Playlist.Workers;

public partial class PlaylistSyncWorker
{
    [LoggerMessage(22800, LogLevel.Information,
        "Playlist sync consumer started (queue {QueueName}, exchange {ExchangeName})")]
    private static partial void LogConsumerStarted(ILogger logger, string queueName, string exchangeName);

    [LoggerMessage(22805, LogLevel.Debug,
        "Queued playlist sync for file {FileId}")]
    private static partial void LogFileQueued(ILogger logger, Guid fileId);

    [LoggerMessage(22806, LogLevel.Debug,
        "Coalesced duplicate playlist sync for file {FileId}")]
    private static partial void LogDuplicateFileQueued(ILogger logger, Guid fileId);

    [LoggerMessage(22807, LogLevel.Warning,
        "Playlist sync buffer full; requeueing file {FileId}")]
    private static partial void LogBufferFull(ILogger logger, Guid fileId);

    [LoggerMessage(22808, LogLevel.Debug,
        "Playlist batch synced for owner {OwnerId}: {Files} file(s), {Playlists} playlist(s), " +
        "{Added} added, {Revived} revived, {Removed} removed")]
    private static partial void LogBatchSynced(
        ILogger logger, Guid ownerId, int files, int playlists, int added, int revived, int removed);

    [LoggerMessage(22802, LogLevel.Warning,
        "Discarding malformed playlist sync message: not a file sync request")]
    private static partial void LogMalformedMessage(ILogger logger);

    [LoggerMessage(22803, LogLevel.Error,
        "Playlist sync failed for owner {OwnerId} and {Files} file(s)")]
    private static partial void LogSyncError(ILogger logger, Exception ex, Guid ownerId, int files);

    [LoggerMessage(22804, LogLevel.Information, "Playlist sync worker stopping")]
    private static partial void LogWorkerStopping(ILogger logger);
}