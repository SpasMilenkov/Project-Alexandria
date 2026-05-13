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

    [LoggerMessage(8004, LogLevel.Debug,
        "Starting video DASH pass for '{InputPath}'")]
    private static partial void LogDashPassStarting(
        ILogger logger, string inputPath);

    [LoggerMessage(8005, LogLevel.Debug,
        "Video DASH pass completed → '{ManifestPath}'")]
    private static partial void LogDashPassCompleted(
        ILogger logger, string manifestPath);
}