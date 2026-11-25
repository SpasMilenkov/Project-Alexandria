using Alexandria.Services.Streaming.VideoEncoding;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public partial class VideoTranspilationService
{
    [LoggerMessage(8000, LogLevel.Information,
        "Video transpilation starting for '{InputPath}' → '{OutputDirectory}'")]
    private static partial void LogTranspilationStarting(
        ILogger logger, string inputPath, string outputDirectory);

    [LoggerMessage(8001, LogLevel.Information,
        "Video transpilation completed for '{InputPath}'")]
    private static partial void LogTranspilationCompleted(
        ILogger logger, string inputPath);

    [LoggerMessage(8009, LogLevel.Information,
        "Source '{InputPath}' shows at {SourceSize}; encoding lanes {Lanes}")]
    private static partial void LogLanesFitted(
        ILogger logger, string inputPath, string sourceSize, string lanes);

    [LoggerMessage(8004, LogLevel.Information,
        "Starting video DASH pass for '{InputPath}' with {Attempt}")]
    private static partial void LogDashPassStarting(
        ILogger logger, string inputPath, VideoEncodeAttempt attempt);

    [LoggerMessage(8005, LogLevel.Debug,
        "Video DASH pass completed → '{ManifestPath}'")]
    private static partial void LogDashPassCompleted(
        ILogger logger, string manifestPath);

    [LoggerMessage(8006, LogLevel.Information,
        "Source '{InputPath}' has no audio track; the stream is published without audio")]
    private static partial void LogSourceWithoutAudio(
        ILogger logger, string inputPath);

    [LoggerMessage(8007, LogLevel.Warning,
        "Video DASH pass with {Failed} failed for '{InputPath}' (exit {ExitCode}: {Reason}); retrying with {Next}")]
    private static partial void LogAttemptFailed(
        ILogger logger, string inputPath, VideoEncodeAttempt failed, VideoEncodeAttempt next, int? exitCode, string reason);

    [LoggerMessage(8008, LogLevel.Information,
        "Constant-quality GPU pass for '{InputPath}' does not fit its ceilings ({Problem}); encoding again capped")]
    private static partial void LogCappedReencode(
        ILogger logger, string inputPath, string problem);
}