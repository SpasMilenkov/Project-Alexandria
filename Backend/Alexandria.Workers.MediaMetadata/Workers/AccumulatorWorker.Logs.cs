using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Workers.MediaMetadata.Workers;

public partial class AccumulatorWorker
{
    [LoggerMessage(22300, LogLevel.Information,
        "Accumulator started: backbone {Backbone}, batch size {BatchSize}, flush wait {Wait}, routing key {RoutingKey}")]
    private static partial void LogAccumulatorStarted(ILogger logger, EssentiaBackbone backbone, int batchSize,
        TimeSpan wait, string routingKey);

    [LoggerMessage(22301, LogLevel.Information,
        "Dispatched batch {BatchId} ({Backbone}, {Count} files) -> {OutputDir} via {RoutingKey}")]
    private static partial void LogBatchDispatched(ILogger logger, Guid batchId, EssentiaBackbone backbone, int count,
        string outputDir, string routingKey);

    [LoggerMessage(22302, LogLevel.Error,
        "Failed to publish dispatch for batch {BatchId} ({Count} files); batch left Dispatched for the timeout sweep")]
    private static partial void LogDispatchError(ILogger logger, Exception ex, Guid batchId, int count);

    [LoggerMessage(22303, LogLevel.Error,
        "Failed to persist batch {BatchId} ({Count} files); triggers released for redelivery")]
    private static partial void LogPersistError(ILogger logger, Exception ex, Guid batchId, int count);

    [LoggerMessage(22304, LogLevel.Error, "Accumulator flush of {Count} files failed unexpectedly")]
    private static partial void LogFlushError(ILogger logger, Exception ex, int count);

    [LoggerMessage(22305, LogLevel.Warning, "Could not settle the trigger delivery for file {FileId}")]
    private static partial void LogSettleError(ILogger logger, Exception ex, Guid fileId);

    [LoggerMessage(22306, LogLevel.Error, "Could not record the media-metadata worker-cycle failure")]
    private static partial void LogRecordFailureError(ILogger logger, Exception ex);
}
