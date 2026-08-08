namespace Alexandria.Workers.MediaMetadata.Workers;

public partial class AutoTagSweepWorker
{
    [LoggerMessage(22600, LogLevel.Information,
        "Auto-tag sweeper started (interval {Interval})")]
    private static partial void LogSweepStarted(ILogger logger, TimeSpan interval);

    [LoggerMessage(22601, LogLevel.Debug,
        "Auto-tag backstop re-queued {Count} file(s)")]
    private static partial void LogBackstopQueued(ILogger logger, int count);

    [LoggerMessage(22602, LogLevel.Debug,
        "Auto-tag retry re-published enrichment for {Count} file(s)")]
    private static partial void LogRetryQueued(ILogger logger, int count);

    [LoggerMessage(22603, LogLevel.Error, "Auto-tag sweep failed")]
    private static partial void LogSweepError(ILogger logger, Exception ex);
}