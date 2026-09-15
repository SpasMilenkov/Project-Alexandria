namespace Alexandria.Workers.Playlist.Workers;

public partial class PlaylistSyncWorker
{
    [LoggerMessage(22800, LogLevel.Information,
        "Playlist sync consumer started (queue {QueueName}, exchange {ExchangeName})")]
    private static partial void LogConsumerStarted(ILogger logger, string queueName, string exchangeName);

    [LoggerMessage(22801, LogLevel.Debug,
        "File {FileId} synced into {Playlists} playlist(s), {Added} item(s) added")]
    private static partial void LogFileSynced(ILogger logger, Guid fileId, int playlists, int added);

    [LoggerMessage(22802, LogLevel.Warning, "Discarding malformed playlist sync message: not a file sync request")]
    private static partial void LogMalformedMessage(ILogger logger);

    [LoggerMessage(22803, LogLevel.Error, "Playlist sync failed for file {FileId}")]
    private static partial void LogSyncError(ILogger logger, Exception ex, Guid fileId);

    [LoggerMessage(22804, LogLevel.Information, "Playlist sync worker stopping")]
    private static partial void LogWorkerStopping(ILogger logger);
}