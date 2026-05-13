namespace Alexandria.Workers.MediaTranspilation;

public partial class TranspilationWorker
{
    [LoggerMessage(12000, LogLevel.Error, "Transpilation consumer ran into an error")]
    private static partial void LogConsumerError(ILogger logger, Exception ex);

    [LoggerMessage(12001, LogLevel.Information, "Transpilation consumer stopping")]
    private static partial void LogWorkerStopping(ILogger logger);
}