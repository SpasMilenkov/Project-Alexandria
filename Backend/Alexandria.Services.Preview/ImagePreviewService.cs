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

public class ImagePreviewService() : IImagePreviewService
{
    public async Task<Stream> GenerateImagePreviewAsync(Stream imageToPreview, string? format, int width = 1280,
        int height = 720)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var stream = new MemoryStream();
        using var image = await Image.LoadAsync(imageToPreview);
        try
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

            var encoder = format is not null
                ? GetEncoder(format)
                : image.Metadata.DecodedImageFormat is not null
                    ? null
                    : GetEncoder(null);

            if (encoder is not null)
                await image.SaveAsync(stream, encoder);
            else
                await image.SaveAsync(stream, image.Metadata.DecodedImageFormat!);

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