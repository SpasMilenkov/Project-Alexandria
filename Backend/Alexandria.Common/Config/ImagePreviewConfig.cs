namespace Alexandria.Common.Config;

/// <summary>
/// Limits and encoder settings for image preview generation.
/// Bind from <c>appsettings.json</c> under the <c>"ImagePreview"</c> section.
/// </summary>
public class ImagePreviewConfig
{
    /// <summary>
    /// Largest single-frame pixel count accepted before decoding. Images above it are rejected
    /// from their header alone.
    /// </summary>
    public long MaxInputPixels { get; set; } = 268_402_689;

    /// <summary>
    /// Animated GIF/WebP sources with more frames than this are previewed from their first frame.
    /// </summary>
    public int MaxAnimatedFrames { get; set; } = 500;

    /// <summary>
    /// Animated sources whose total pixel count (all frames) exceeds this are previewed from their
    /// first frame.
    /// </summary>
    public long MaxAnimatedPixels { get; set; } = 100_000_000;

    /// <summary>
    /// Wall-clock budget for generating both artifacts of one image.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// libvips worker threads per operation. Zero keeps the libvips default (one per core).
    /// </summary>
    public int Concurrency { get; set; }

    public int JpegQuality { get; set; } = 85;

    public int WebpQuality { get; set; } = 85;

    public ImageMagickConfig Magick { get; set; } = new();
}
