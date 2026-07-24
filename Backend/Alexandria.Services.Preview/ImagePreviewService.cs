using Alexandria.Common.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Alexandria.Services.Preview;

public class ImagePreviewService : IImagePreviewService
{
    public async Task<Stream> GenerateImagePreviewAsync(Stream imageToPreview, string? format, int width = 1280,
        int height = 720, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        return await ProcessImageAsync(imageToPreview, format, image =>
        {
            if (image.Width > width || image.Height > height)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(width, height),
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Welch
                }));
            }
        }, ct);
    }

    public async Task<Stream> GenerateImageThumbnailAsync(Stream imageToPreview, string? format, int size = 320,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        return await ProcessImageAsync(imageToPreview, format, image =>
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = ResizeMode.Pad,
                PadColor = Color.Transparent,
                Sampler = KnownResamplers.Welch
            }));
        }, ct);
    }

    private static async Task<Stream> ProcessImageAsync(
        Stream imageStream,
        string? format,
        Action<Image> mutateAction,
        CancellationToken ct)
    {
        var stream = new MemoryStream();
        using var image = await Image.LoadAsync(imageStream, ct);
        try
        {
            mutateAction(image);

            var encoder = format is not null
                ? GetEncoder(format)
                : image.Metadata.DecodedImageFormat is not null
                    ? null
                    : GetEncoder(null);

            if (encoder is not null)
                await image.SaveAsync(stream, encoder, ct);
            else
                await image.SaveAsync(stream, image.Metadata.DecodedImageFormat!, ct);

            stream.Position = 0;
            return stream;
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    private static IImageEncoder GetEncoder(string? format)
    {
        return format switch
        {
            "image/jpeg" => new JpegEncoder() { Quality = 85 },
            "image/png" => new PngEncoder(),
            "image/gif" => new GifEncoder(),
            "image/bmp" => new BmpEncoder(),
            "image/webp" => new WebpEncoder() { Quality = 85 },
            "image/x-tga" => new TgaEncoder(),
            _ => new PngEncoder() // Safe fallback
        };
    }
}