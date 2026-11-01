using System.Globalization;
using Alexandria.Common.Config;

namespace Alexandria.Services.Preview.Images;

internal sealed record ImageOutputFormat(string MimeType, string Suffix, bool SupportsAlpha)
{
    public static readonly ImageOutputFormat Jpeg = new("image/jpeg", ".jpg", false);
    public static readonly ImageOutputFormat Png = new("image/png", ".png", true);
    public static readonly ImageOutputFormat Webp = new("image/webp", ".webp", true);
    public static readonly ImageOutputFormat Gif = new("image/gif", ".gif", true);

    private static readonly Dictionary<string, ImageOutputFormat> Passthrough =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = Jpeg,
            ["image/jpg"] = Jpeg,
            ["image/pjpeg"] = Jpeg,
            ["image/png"] = Png,
            ["image/webp"] = Webp,
            ["image/gif"] = Gif
        };

    /// <summary>
    /// Browser-safe sources keep their format; everything else (SVG, TIFF, AVIF, HEIC, BMP, ...)
    /// is re-encoded as WebP so the artifact can always be shown inline.
    /// </summary>
    public static ImageOutputFormat ForPreview(string? sourceMimeType)
    {
        if (sourceMimeType is not null && Passthrough.TryGetValue(sourceMimeType, out var format))
            return format;

        return Webp;
    }

    /// <summary>
    /// Thumbnails are padded with transparency, so formats without alpha fall back to WebP.
    /// </summary>
    public static ImageOutputFormat ForThumbnail(string? sourceMimeType)
    {
        var format = ForPreview(sourceMimeType);
        return format.SupportsAlpha ? format : Webp;
    }

    public string SaveOptions(ImagePreviewConfig config)
    {
        if (this == Jpeg)
            return string.Create(CultureInfo.InvariantCulture, $"{Suffix}[Q={config.JpegQuality},keep=none]");

        if (this == Webp)
            return string.Create(CultureInfo.InvariantCulture, $"{Suffix}[Q={config.WebpQuality},keep=none]");

        if (this == Png)
            return $"{Suffix}[filter=all,keep=none]";

        // Only GIF sources produce GIF output, so reusing the source palette skips per-frame
        // quantization, which otherwise dominates animated GIF encoding time.
        return $"{Suffix}[keep=none,reuse=true,interframe_maxerror=8]";
    }
}
