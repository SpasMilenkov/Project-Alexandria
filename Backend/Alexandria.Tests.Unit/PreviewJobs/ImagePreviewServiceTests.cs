using System.Text;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions.Preview.Images;
using Alexandria.Common.Services;
using Alexandria.Services.Preview;
using Alexandria.Services.Preview.Images;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetVips;

namespace Alexandria.Tests.Unit.PreviewJobs;

public class ImagePreviewServiceTests
{
    private static ImagePreviewService CreateSut(Action<ImagePreviewConfig>? configure = null)
    {
        var config = new ImagePreviewConfig();
        config.Magick.Enabled = false;
        configure?.Invoke(config);

        var options = Options.Create(config);
        return new ImagePreviewService(
            options,
            new MagickCliDecoder(options, NullLogger<MagickCliDecoder>.Instance),
            NullLogger<ImagePreviewService>.Instance);
    }

    private static byte[] Solid(int width, int height, string suffix, int bands = 3)
    {
        using var black = Image.Black(width, height, bands);
        using var grey = black.Linear([1.0], [128.0]);
        using var image = grey.Cast(Enums.BandFormat.Uchar);
        return image.WriteToBuffer(suffix);
    }

    private static byte[] AnimatedGif(int frames, int size = 100)
    {
        var pages = new Image[frames];
        try
        {
            for (var i = 0; i < frames; i++)
            {
                using var black = Image.Black(size, size, 3);
                using var shaded = black.Linear([1.0], [40.0 * i]);
                pages[i] = shaded.Cast(Enums.BandFormat.Uchar);
            }

            using var strip = Image.Arrayjoin(pages, across: 1);
            using var animated = strip.Mutate(m =>
            {
                m.Set(GValue.GIntType, "page-height", size);
                m.Set(GValue.ArrayIntType, "delay", Enumerable.Repeat(100, frames).ToArray());
            });
            return animated.WriteToBuffer(".gif");
        }
        finally
        {
            foreach (var page in pages)
                page?.Dispose();
        }
    }

    private static Image Decode(ImagePreviewResult result)
    {
        result.Content.Position = 0;
        using var buffer = new MemoryStream();
        result.Content.CopyTo(buffer);

        var bytes = buffer.ToArray();
        var loader = Image.FindLoadBuffer(bytes) ?? "";
        var multiPage = loader.Contains("gif", StringComparison.OrdinalIgnoreCase) ||
                        loader.Contains("webp", StringComparison.OrdinalIgnoreCase);

        return Image.NewFromBuffer(bytes, multiPage ? "n=-1" : "");
    }

    private static int Pages(Image image) =>
        image.Contains("n-pages") ? (int)image.Get("n-pages") : 1;

    private static async Task<ImagePreviewArtifacts> Generate(
        ImagePreviewService sut, byte[] bytes, string? mimeType) =>
        await sut.GenerateAsync(new MemoryStream(bytes), mimeType, TestContext.Current.CancellationToken);

    [Fact]
    public async Task GenerateAsync_largeJpeg_previewFitsBoxAndKeepsJpeg()
    {
        var sut = CreateSut();

        await using var result = await Generate(sut, Solid(2000, 1000, ".jpg"), "image/jpeg");

        result.Preview.ContentType.Should().Be("image/jpeg");
        using var preview = Decode(result.Preview);
        preview.Width.Should().Be(1280);
        preview.Height.Should().Be(640);
    }

    [Fact]
    public async Task GenerateAsync_smallImage_previewIsNotUpscaled()
    {
        var sut = CreateSut();

        await using var result = await Generate(sut, Solid(300, 200, ".png"), "image/png");

        result.Preview.ContentType.Should().Be("image/png");
        using var preview = Decode(result.Preview);
        preview.Width.Should().Be(300);
        preview.Height.Should().Be(200);
    }

    [Fact]
    public async Task GenerateAsync_wideImage_thumbnailIsTransparentlyPaddedSquare()
    {
        var sut = CreateSut();

        await using var result = await Generate(sut, Solid(2000, 1000, ".png"), "image/png");

        result.Thumbnail.ContentType.Should().Be("image/png");
        using var thumbnail = Decode(result.Thumbnail);
        thumbnail.Width.Should().Be(320);
        thumbnail.Height.Should().Be(320);
        thumbnail.HasAlpha().Should().BeTrue();
        thumbnail.Getpoint(0, 0)[3].Should().Be(0);
        thumbnail.Getpoint(160, 160)[3].Should().Be(255);
    }

    [Fact]
    public async Task GenerateAsync_jpegSource_thumbnailIsEncodedAsWebp()
    {
        var sut = CreateSut();

        await using var result = await Generate(sut, Solid(800, 600, ".jpg"), "image/jpeg");

        result.Thumbnail.ContentType.Should().Be("image/webp");
        using var thumbnail = Decode(result.Thumbnail);
        thumbnail.HasAlpha().Should().BeTrue();
    }

    [Theory]
    [InlineData(".tif", "image/tiff")]
    [InlineData(".avif", "image/avif")]
    public async Task GenerateAsync_nonPassthroughFormat_isReencodedAsWebp(string suffix, string mimeType)
    {
        var sut = CreateSut();

        await using var result = await Generate(sut, Solid(640, 480, suffix), mimeType);

        result.Preview.ContentType.Should().Be("image/webp");
        result.Thumbnail.ContentType.Should().Be("image/webp");
        using var preview = Decode(result.Preview);
        preview.Width.Should().Be(640);
    }

    [Fact]
    public async Task GenerateAsync_svg_isRasterizedToWebp()
    {
        var sut = CreateSut();
        var svg = Encoding.UTF8.GetBytes(
            """<svg xmlns="http://www.w3.org/2000/svg" width="800" height="500"><rect width="800" height="500" fill="#143d59"/></svg>""");

        await using var result = await Generate(sut, svg, "image/svg+xml");

        result.Preview.ContentType.Should().Be("image/webp");
        using var preview = Decode(result.Preview);
        preview.Width.Should().Be(800);
        preview.Height.Should().Be(500);
    }

    [Fact]
    public async Task GenerateAsync_svgWithExternalFileReference_doesNotEmbedTheFile()
    {
        var sut = CreateSut();
        var red = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        await using (var file = File.Create(red))
        {
            using var black = Image.Black(100, 100, 3);
            using var image = black.Linear([1, 1, 1], [255, 0, 0]).Cast(Enums.BandFormat.Uchar);
            await file.WriteAsync(image.WriteToBuffer(".png"), TestContext.Current.CancellationToken);
        }

        try
        {
            var svg = Encoding.UTF8.GetBytes(
                $"""<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><rect width="100" height="100" fill="#000080"/><image href="file://{red}" width="100" height="100"/></svg>""");

            await using var result = await Generate(sut, svg, "image/svg+xml");

            using var preview = Decode(result.Preview);
            var centre = preview.Getpoint(50, 50);
            centre[0].Should().BeLessThan(10);
            centre[2].Should().BeGreaterThan(100);
        }
        finally
        {
            File.Delete(red);
        }
    }

    [Fact]
    public async Task GenerateAsync_svgWithExternalEntity_fails()
    {
        var sut = CreateSut();
        var svg = Encoding.UTF8.GetBytes(
            """
            <?xml version="1.0"?>
            <!DOCTYPE svg [ <!ENTITY xxe SYSTEM "file:///etc/hostname"> ]>
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><text x="5" y="50">&xxe;</text></svg>
            """);

        var act = () => Generate(sut, svg, "image/svg+xml");

        await act.Should().ThrowAsync<VipsException>();
    }

    [Fact]
    public async Task GenerateAsync_exifOrientation_isAppliedAndMetadataStripped()
    {
        var sut = CreateSut();
        byte[] rotated;
        using (var black = Image.Black(400, 200, 3))
        using (var image = black.Mutate(m => m.Set(GValue.GIntType, "orientation", 6)))
            rotated = image.WriteToBuffer(".jpg");

        await using var result = await Generate(sut, rotated, "image/jpeg");

        using var preview = Decode(result.Preview);
        preview.Width.Should().Be(200);
        preview.Height.Should().Be(400);
        preview.Contains("exif-data").Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_animatedGifUnderCaps_keepsEveryFrame()
    {
        var sut = CreateSut();

        await using var result = await Generate(sut, AnimatedGif(3), "image/gif");

        result.Preview.ContentType.Should().Be("image/gif");
        using var preview = Decode(result.Preview);
        Pages(preview).Should().Be(3);

        using var thumbnail = Decode(result.Thumbnail);
        Pages(thumbnail).Should().Be(3);
        thumbnail.Width.Should().Be(320);
        thumbnail.PageHeight.Should().Be(320);
    }

    [Fact]
    public async Task GenerateAsync_gifWithinPreviewBox_previewIsSourceBytes()
    {
        var sut = CreateSut();
        var gif = AnimatedGif(3);

        await using var result = await Generate(sut, gif, "image/gif");

        ((MemoryStream)result.Preview.Content).ToArray().Should().Equal(gif);
    }

    [Fact]
    public async Task GenerateAsync_gifThumbnail_keepsTransparentPadding()
    {
        var sut = CreateSut();

        await using var result = await Generate(sut, Solid(300, 100, ".gif"), "image/gif");

        using var thumbnail = Decode(result.Thumbnail);
        thumbnail.HasAlpha().Should().BeTrue();
        thumbnail.Getpoint(0, 0)[3].Should().Be(0);
        thumbnail.Getpoint(160, 160)[3].Should().Be(255);
    }

    [Fact]
    public async Task GenerateAsync_animatedGifOverFrameCap_usesFirstFrame()
    {
        var sut = CreateSut(c => c.MaxAnimatedFrames = 2);

        await using var result = await Generate(sut, AnimatedGif(3), "image/gif");

        using var preview = Decode(result.Preview);
        Pages(preview).Should().Be(1);
        using var thumbnail = Decode(result.Thumbnail);
        Pages(thumbnail).Should().Be(1);
    }

    [Fact]
    public async Task GenerateAsync_overPixelLimit_isRejectedWithoutFallback()
    {
        var sut = CreateSut(c => c.MaxInputPixels = 1000);

        var act = () => Generate(sut, Solid(100, 100, ".png"), "image/png");

        await act.Should().ThrowAsync<ImageTooLargeException>();
    }

    [Fact]
    public async Task GenerateAsync_noLoaderAndFallbackDisabled_throwsUnsupportedFormat()
    {
        var sut = CreateSut();

        var act = () => Generate(sut, "BM not really a bitmap"u8.ToArray(), "image/bmp");

        await act.Should().ThrowAsync<UnsupportedImageFormatException>();
    }

    [Fact]
    public async Task GenerateAsync_blockedLoader_isNotUsed()
    {
        var sut = CreateSut();

        var act = () => Generate(sut, "1,2,3\n4,5,6\n"u8.ToArray(), "text/csv");

        await act.Should().ThrowAsync<UnsupportedImageFormatException>();
    }

    [Fact]
    public async Task GenerateAsync_nonSeekableSource_throws()
    {
        var sut = CreateSut();
        await using var source = new NonSeekableStream(Solid(10, 10, ".png"));

        var act = () => sut.GenerateAsync(source, "image/png", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateAsync_cancelledToken_throwsOperationCanceled()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => sut.GenerateAsync(new MemoryStream(Solid(500, 500, ".png")), "image/png", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }
}
