namespace Alexandria.Workers.MediaTranspilation;

public partial class TranscodeBackfillWorker
{
    [LoggerMessage(12100, LogLevel.Information,
        "Transcode backfill consumer started (queue {QueueName}, exchange {ExchangeName})")]
    private static partial void LogConsumerStarted(ILogger logger, string queueName, string exchangeName);

    [LoggerMessage(12101, LogLevel.Warning, "Discarding malformed transcode backfill request: {Body}")]
    private static partial void LogMalformedMessage(ILogger logger, string body);

    [LoggerMessage(12102, LogLevel.Debug, "Transcode backfill request body is not valid JSON")]
    private static partial void LogUnreadableBody(ILogger logger, Exception ex);

    [LoggerMessage(12103, LogLevel.Error, "Transcode backfill failed for rule {RuleId} of policy {PolicyId}")]
    private static partial void LogBackfillFailed(ILogger logger, Exception ex, Guid policyId, Guid ruleId);

    [LoggerMessage(12104, LogLevel.Information,
        "Stopping mid-backfill; delivery {DeliveryTag} goes back to the queue when the channel closes.")]
    private static partial void LogStoppingMidMessage(ILogger logger, ulong deliveryTag);

    [LoggerMessage(12105, LogLevel.Warning, "Could not record the incident for a dropped transcode backfill request")]
    private static partial void LogIncidentRecordFailed(ILogger logger, Exception ex);

    [LoggerMessage(12106, LogLevel.Information, "Transcode backfill consumer stopping")]
    private static partial void LogWorkerStopping(ILogger logger);
}
