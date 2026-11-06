namespace Alexandria.Workers.Lyrics;

public partial class LyricsWorker
{
    [LoggerMessage(22000, LogLevel.Error, "Lyrics consumer ran into an error")]
    private static partial void LogConsumerError(ILogger logger, Exception ex);

    [LoggerMessage(22002, LogLevel.Error,
        "Delivery failed {Attempts} times for lyrics trigger; parked, failure event recorded")]
    private static partial void LogTriggerParked(ILogger logger, Exception ex, int attempts);

    [LoggerMessage(22003, LogLevel.Error, "Failed to park poison lyrics trigger; requeueing")]
    private static partial void LogParkFailed(ILogger logger, Exception ex);

    [LoggerMessage(22004, LogLevel.Warning, "Malformed lyrics trigger parked: {Message}")]
    private static partial void LogMalformedParked(ILogger logger, string message);

    [LoggerMessage(22001, LogLevel.Information, "Lyrics consumer stopping")]
    private static partial void LogWorkerStopping(ILogger logger);

    [LoggerMessage(22005, LogLevel.Information,
        "Stopping mid-message; delivery {DeliveryTag} goes back to the queue when the channel closes.")]
    private static partial void LogStoppingMidMessage(ILogger logger, ulong deliveryTag);
}