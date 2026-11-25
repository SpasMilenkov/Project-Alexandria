using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming.VideoEncoding;

public sealed partial class VideoEncoderSelector
{
    [LoggerMessage(8010, LogLevel.Information, "Video encoding on the GPU {Device} ({Driver})")]
    private static partial void LogGpuSelected(ILogger logger, string device, string driver);

    [LoggerMessage(8011, LogLevel.Warning,
        "GPU {Device} is not usable, so video is encoded on the CPU: {Detail}")]
    private static partial void LogGpuUnusable(ILogger logger, string device, string detail);

    [LoggerMessage(8012, LogLevel.Information, "Video encoding on the CPU: no GPU at {Device}")]
    private static partial void LogNoGpu(ILogger logger, string device);

    [LoggerMessage(8013, LogLevel.Information, "Video encoding on the CPU: GPU encoding is turned off")]
    private static partial void LogCpuConfigured(ILogger logger);

    [LoggerMessage(8014, LogLevel.Error, "The GPU probe for {Device} failed unexpectedly, so video is encoded on the CPU")]
    private static partial void LogProbeFailed(ILogger logger, Exception exception, string device);
}
