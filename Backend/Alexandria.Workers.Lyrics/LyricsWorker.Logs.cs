namespace Alexandria.Workers.Lyrics;

public partial class LyricsWorker
{
    [LoggerMessage(22000, LogLevel.Error, "Lyrics consumer ran into an error")]
    private static partial void LogConsumerError(ILogger logger, Exception ex);

    [LoggerMessage(22001, LogLevel.Information, "Lyrics consumer stopping")]
    private static partial void LogWorkerStopping(ILogger logger);
}