namespace Alexandria.Workers.MediaTranspilation;

public partial class TranspilationWorker
{
    [LoggerMessage(12000, LogLevel.Error, "Transpilation consumer ran into an error")]
    private static partial void LogConsumerError(ILogger logger, Exception ex);

    [LoggerMessage(12001, LogLevel.Information, "Transpilation consumer stopping")]
    private static partial void LogWorkerStopping(ILogger logger);

    [LoggerMessage(12002, LogLevel.Warning, "Discarding transpilation message without a valid job id")]
    private static partial void LogMalformedMessage(ILogger logger);
}
