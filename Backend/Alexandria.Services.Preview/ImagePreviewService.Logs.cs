using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview;

public partial class ImagePreviewService
{
    [LoggerMessage(4201, LogLevel.Warning,
        "libvips ({Loader}) could not decode {MimeType}; retrying with the ImageMagick fallback")]
    private static partial void LogVipsFallback(ILogger logger, Exception ex, string loader, string mimeType);

    [LoggerMessage(4202, LogLevel.Debug,
        "Animated image with {Pages} frames ({TotalPixels} pixels) exceeds the animation caps; using the first frame")]
    private static partial void LogAnimationCapped(ILogger logger, int pages, long totalPixels);
}
