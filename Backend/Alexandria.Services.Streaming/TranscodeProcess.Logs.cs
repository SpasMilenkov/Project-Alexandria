using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

internal static partial class TranscodeProcess
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Cancellation requested for transcode job {JobId}, stopping ffmpeg.")]
    private static partial void LogJobCancellationRequested(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Polling the status of transcode job {JobId} failed, will try again.")]
    private static partial void LogWatchdogPollFailed(ILogger logger, Exception ex, Guid jobId);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Stopped watching transcode job {JobId} for cancellation.")]
    private static partial void LogWatchdogStopped(ILogger logger, Guid jobId);
}
