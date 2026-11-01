using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Images;

public sealed partial class MagickCliDecoder
{
    [LoggerMessage(4250, LogLevel.Information, "ImageMagick fallback decoder available: {Version}")]
    private static partial void LogAvailable(ILogger logger, string version);

    [LoggerMessage(4251, LogLevel.Warning,
        "ImageMagick fallback decoder unavailable, formats libvips cannot load will not get previews: {Reason}")]
    private static partial void LogUnavailable(ILogger logger, string reason);

    [LoggerMessage(4252, LogLevel.Debug, "Decoding {Coder} source of {Bytes} bytes with ImageMagick")]
    private static partial void LogDecodeStarting(ILogger logger, string coder, long bytes);

    [LoggerMessage(4253, LogLevel.Debug, "ImageMagick decoded {Coder} source into {Bytes} bytes of PNG")]
    private static partial void LogDecodeCompleted(ILogger logger, string coder, int bytes);
}
