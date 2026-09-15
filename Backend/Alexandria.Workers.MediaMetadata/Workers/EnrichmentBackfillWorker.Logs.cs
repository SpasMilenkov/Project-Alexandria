namespace Alexandria.Workers.MediaMetadata.Workers;

public partial class EnrichmentBackfillWorker
{
    [LoggerMessage(22700, LogLevel.Information,
        "Enrichment backfill consumer started (queue {QueueName}, exchange {ExchangeName})")]
    private static partial void LogConsumerStarted(ILogger logger, string queueName, string exchangeName);

    [LoggerMessage(22701, LogLevel.Information,
        "Backfill drained for policy {PolicyId}: {Published}/{Candidates} enrich trigger(s)")]
    private static partial void LogBackfillDrained(ILogger logger, Guid policyId, int published, int candidates);

    [LoggerMessage(22702, LogLevel.Warning,
        "Discarding malformed backfill message '{Message}': not a policy id")]
    private static partial void LogMalformedMessage(ILogger logger, string message);

    [LoggerMessage(22703, LogLevel.Error, "Enrichment backfill failed for policy {PolicyId}")]
    private static partial void LogBackfillError(ILogger logger, Exception ex, Guid policyId);

    [LoggerMessage(22704, LogLevel.Information, "Enrichment backfill worker stopping")]
    private static partial void LogWorkerStopping(ILogger logger);
}