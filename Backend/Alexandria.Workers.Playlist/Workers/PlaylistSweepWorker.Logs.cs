namespace Alexandria.Workers.Playlist.Workers;

public partial class PlaylistSweepWorker
{
    [LoggerMessage(22810, LogLevel.Information,
        "Playlist sweep started (interval {Interval})")]
    private static partial void LogSweepStarted(ILogger logger, TimeSpan interval);

    [LoggerMessage(22812, LogLevel.Information,
        "Playlist sweep drained: {Owners} owner(s), {Playlists} playlist(s), {Added} item(s) added")]
    private static partial void LogSweepDrained(ILogger logger, int owners, int playlists, int added);

    [LoggerMessage(22811, LogLevel.Error, "Playlist sweep failed")]
    private static partial void LogSweepError(ILogger logger, Exception ex);
}