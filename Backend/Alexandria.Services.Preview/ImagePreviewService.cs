using Alexandria.Common.Config;
using Alexandria.Common.Exceptions.Preview.Images;
using Alexandria.Common.Services;
using Alexandria.Services.Preview.Images;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetVips;

namespace Alexandria.Services.Preview;

public partial class ImagePreviewService : IImagePreviewService
{
    private const int PreviewWidth = 1280;
    private const int PreviewHeight = 720;
    private const int ThumbnailSize = 320;

    private static readonly double[] TransparentBackground = [0, 0, 0, 0];
    private static readonly double[] OpaqueAlpha = [255];

    private readonly ImagePreviewConfig _config;
    private readonly MagickCliDecoder _magick;
    private readonly ILogger<ImagePreviewService> _logger;

    public ImagePreviewService(
        IOptions<ImagePreviewConfig> options,
        MagickCliDecoder magick,
        ILogger<ImagePreviewService> logger)
    {
        _config = options.Value;
        _magick = magick;
        _logger = logger;

        VipsRuntime.EnsureConfigured(_config);
    }

    /// <inheritdoc/>
    public async Task<ImagePreviewArtifacts> GenerateAsync(Stream source, string? mimeType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanSeek)
            throw new ArgumentException("Image preview source must be seekable.", nameof(source));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));

        try
        {
            return await GenerateCoreAsync(source, mimeType, timeout.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or VipsException &&
                                   ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (Exception ex) when (ex is OperationCanceledException or VipsException &&
                                   timeout.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Image preview generation exceeded its {_config.TimeoutSeconds}s time limit.", ex);
        }
    }

    private async Task<ImagePreviewArtifacts> GenerateCoreAsync(Stream source, string? mimeType,
        CancellationToken ct)
    {
        var coder = MagickCliDecoder.CoderFor(mimeType);

        source.Position = 0;
        var loader = Image.FindLoadStream(source);
        source.Position = 0;

        if (loader is not null)
        {
            try
            {
                return await Task.Run(() => RenderFromStream(source, loader, mimeType, ct), ct);
            }
            catch (VipsException ex) when (!ct.IsCancellationRequested && coder is not null)
            {
                LogVipsFallback(_logger, ex, loader, mimeType ?? "unknown");
            }
        }

        if (coder is null || !await _magick.IsAvailableAsync())
            throw new UnsupportedImageFormatException(mimeType);

        var decoded = await _magick.DecodeToPngAsync(source, coder, ct);
        return await Task.Run(() => RenderFromBuffer(decoded, ct), ct);
    }

    private ImagePreviewArtifacts RenderFromStream(Stream source, string loader, string? mimeType,
        CancellationToken ct)
    {
        ImageHeader header;
        using (var probe = Image.NewFromStream(source, access: Enums.Access.Sequential))
            header = ReadHeader(probe, loader);

        EnsureWithinPixelLimit(header);
        var loadOptions = ResolveLoadOptions(header);
        var previewFormat = ImageOutputFormat.ForPreview(mimeType);

        var preview = CanPassThroughPreview(header, loadOptions, previewFormat)
            ? CopySource(source, previewFormat)
            : null;

        return Render(
            (width, height, size) =>
            {
                source.Position = 0;
                return Image.ThumbnailStream(source, width, loadOptions, height, size);
            },
            previewFormat,
            ImageOutputFormat.ForThumbnail(mimeType),
            preview,
            ct);
    }

    private ImagePreviewArtifacts RenderFromBuffer(byte[] decoded, CancellationToken ct)
    {
        using (var probe = Image.NewFromBuffer(decoded, access: Enums.Access.Sequential))
            EnsureWithinPixelLimit(ReadHeader(probe, "VipsForeignLoadPngBuffer"));

        return Render(
            (width, height, size) => Image.ThumbnailBuffer(decoded, width, height: height, size: size),
            ImageOutputFormat.Webp,
            ImageOutputFormat.Webp,
            null,
            ct);
    }

    private ImagePreviewArtifacts Render(
        Func<int, int, Enums.Size, Image> thumbnail,
        ImageOutputFormat previewFormat,
        ImageOutputFormat thumbnailFormat,
        ImagePreviewResult? preview,
        CancellationToken ct)
    {
        if (preview is null)
        {
            using var resized = thumbnail(PreviewWidth, PreviewHeight, Enums.Size.Down);
            using var normalized = ToSrgb(resized);
            preview = Encode(normalized, previewFormat, ct);
        }

        using (var resized = thumbnail(ThumbnailSize, ThumbnailSize, Enums.Size.Both))
        using (var normalized = ToSrgb(resized))
        using (var padded = Pad(normalized, ThumbnailSize))
            return new ImagePreviewArtifacts(preview, Encode(padded, thumbnailFormat, ct));
    }

    /// <summary>
    /// GIFs carry no EXIF/GPS metadata, so one that already fits the preview box is served as
    /// uploaded. Re-quantizing every frame is by far the most expensive part of animated GIFs.
    /// </summary>
    private static bool CanPassThroughPreview(ImageHeader header, string? loadOptions, ImageOutputFormat format) =>
        format == ImageOutputFormat.Gif &&
        (header.Pages == 1 || loadOptions is not null) &&
        header.Width <= PreviewWidth &&
        header.PageHeight <= PreviewHeight;

    private static ImagePreviewResult CopySource(Stream source, ImageOutputFormat format)
    {
        var copy = new MemoryStream();
        source.Position = 0;
        source.CopyTo(copy);
        copy.Position = 0;

        return new ImagePreviewResult(copy, format.MimeType);
    }

    private static ImageHeader ReadHeader(Image probe, string loader)
    {
        var pages = probe.Contains("n-pages") ? (int)probe.Get("n-pages") : 1;
        return new ImageHeader(loader, probe.Width, probe.PageHeight, Math.Max(pages, 1));
    }

    private void EnsureWithinPixelLimit(ImageHeader header)
    {
        if (header.FramePixels > _config.MaxInputPixels)
            throw new ImageTooLargeException(header.Width, header.PageHeight, _config.MaxInputPixels);
    }

    private string? ResolveLoadOptions(ImageHeader header)
    {
        if (!header.IsAnimatable)
            return null;

        if (header.Pages > _config.MaxAnimatedFrames || header.TotalPixels > _config.MaxAnimatedPixels)
        {
            LogAnimationCapped(_logger, header.Pages, header.TotalPixels);
            return null;
        }

        return "n=-1";
    }

    private static Image ToSrgb(Image image)
    {
        using var srgb = image.Interpretation == Enums.Interpretation.Srgb
            ? image.Copy()
            : image.Colourspace(Enums.Interpretation.Srgb);

        return srgb.Format == Enums.BandFormat.Uchar
            ? srgb.Copy()
            : srgb.Cast(Enums.BandFormat.Uchar);
    }

    /// <summary>
    /// Centres every frame on a transparent square. Animated images are a vertical strip of
    /// frames, so each page is padded on its own and the strip is rebuilt.
    /// </summary>
    private static Image Pad(Image image, int size)
    {
        using var withAlpha = image.HasAlpha() ? image.Copy() : image.BandjoinConst(OpaqueAlpha);

        var pageHeight = withAlpha.PageHeight;
        var pages = withAlpha.Height / pageHeight;

        if (pages <= 1)
            return PadFrame(withAlpha, size);

        var frames = new Image[pages];
        try
        {
            for (var i = 0; i < pages; i++)
            {
                using var page = withAlpha.Crop(0, i * pageHeight, withAlpha.Width, pageHeight);
                frames[i] = PadFrame(page, size);
            }

            using var joined = Image.Arrayjoin(frames, across: 1);
            return joined.Mutate(m => m.Set(GValue.GIntType, "page-height", size));
        }
        finally
        {
            foreach (var frame in frames)
                frame?.Dispose();
        }
    }

    private static Image PadFrame(Image frame, int size) =>
        frame.Gravity(Enums.CompassDirection.Centre, size, size, Enums.Extend.Background, TransparentBackground);

    private ImagePreviewResult Encode(Image image, ImageOutputFormat format, CancellationToken ct)
    {
        // Pixels are only computed here, so this is where cancellation can actually interrupt work.
        image.SetProgress(NullProgress.Instance, ct);

        var bytes = image.WriteToBuffer(format.SaveOptions(_config));
        return new ImagePreviewResult(new MemoryStream(bytes, writable: false), format.MimeType);
    }

    private sealed class NullProgress : IProgress<int>
    {
        public static readonly NullProgress Instance = new();

        public void Report(int value)
        {
        }
    }
}
