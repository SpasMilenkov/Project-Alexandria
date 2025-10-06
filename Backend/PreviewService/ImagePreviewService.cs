using Common.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;


namespace PreviewService;

public class ImagePreviewService(): IImagePreviewService
{
    public async Task<Stream> GenerateImagePreview(Stream imageToPreview,  string? format, int width = 1280, int height = 720)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        var stream = new MemoryStream();
        using var image = await Image.LoadAsync(imageToPreview);
        try
        {

            image.Mutate(x => x.Resize(width, height, KnownResamplers.Welch));

            if (format is null && image.Metadata.DecodedImageFormat is not null)
                await image.SaveAsync(stream, image.Metadata.DecodedImageFormat);
            else
            {
                var encoder = GetEncoder(format);
                await image.SaveAsync(stream, encoder);
            }

            stream.Position = 0;

            return stream;
        }
        catch (Exception e)
        {
            await stream.DisposeAsync();
            throw;
        }
    }
    
    private static IImageEncoder GetEncoder(string format)
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