using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public partial class AudioTranspilationService
{
    [LoggerMessage(9000, LogLevel.Information,
        "Audio transpilation starting for '{InputPath}' → '{OutputDirectory}'")]
    private static partial void LogTranspilationStarting(
        ILogger logger, string inputPath, string outputDirectory);

    [LoggerMessage(9001, LogLevel.Information,
        "Audio transpilation completed for '{InputPath}'")]
    private static partial void LogTranspilationCompleted(
        ILogger logger, string inputPath);

    [LoggerMessage(9004, LogLevel.Debug,
        "Starting audio DASH pass for '{InputPath}'")]
    private static partial void LogDashPassStarting(
        ILogger logger, string inputPath);

    [LoggerMessage(9005, LogLevel.Debug,
        "Audio DASH pass completed → '{ManifestPath}'")]
    private static partial void LogDashPassCompleted(
        ILogger logger, string manifestPath);
}