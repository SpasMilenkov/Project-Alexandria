using System.Buffers.Binary;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions.Preview.Images;
using Alexandria.Services.Preview;
using Alexandria.Services.Preview.Images;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetVips;

namespace Alexandria.Tests.Unit.PreviewJobs;

public class MagickCliDecoderTests
{
    // IM7 ships `magick`; IM6 (common on developer machines) only has `convert`, which accepts
    // the same arguments used here.
    private static readonly string? MagickBinary = new[] { "magick", "convert" }.FirstOrDefault(OnPath);

    private static bool OnPath(string binary) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator)
        .Any(dir => File.Exists(Path.Combine(dir, binary)));

    private static ImagePreviewConfig CreateConfig(Action<ImageMagickConfig>? configure = null)
    {
        var config = new ImagePreviewConfig();
        config.Magick.BinaryPath = MagickBinary ?? "magick";
        configure?.Invoke(config.Magick);
        return config;
    }

    private static MagickCliDecoder CreateSut(ImagePreviewConfig config) =>
        new(Options.Create(config), NullLogger<MagickCliDecoder>.Instance);

    /// <summary>
    /// Builds an uncompressed 24-bit BMP, a format the bundled libvips cannot load.
    /// </summary>
    private static byte[] Bitmap(int width, int height)
    {
        var rowSize = (width * 3 + 3) & ~3;
        var pixelBytes = rowSize * height;
        var data = new byte[54 + pixelBytes];

        data[0] = (byte)'B';
        data[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(2), data.Length);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(10), 54);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(18), width);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(22), height);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(26), 1);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(28), 24);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(34), pixelBytes);

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            data[54 + y * rowSize + x * 3 + 2] = 255;

        return data;
    }

    [Theory]
    [InlineData("image/bmp", "BMP")]
    [InlineData("IMAGE/X-TGA", "TGA")]
    [InlineData("image/heic", "HEIC")]
    [InlineData("image/vnd.adobe.photoshop", "PSD")]
    [InlineData("image/x-icon", "ICO")]
    public void CoderFor_allowlistedMimeType_returnsCoder(string mimeType, string expected)
    {
        MagickCliDecoder.CoderFor(mimeType).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("image/svg+xml")]
    [InlineData("application/pdf")]
    [InlineData("application/postscript")]
    public void CoderFor_notAllowlisted_returnsNull(string? mimeType)
    {
        MagickCliDecoder.CoderFor(mimeType).Should().BeNull();
    }

    [Fact]
    public void BuildDecodeArguments_wrapsMagickInPrlimitAndPinsTheCoder()
    {
        var config = CreateConfig(c => c.BinaryPath = "magick");

        var arguments = MagickCliDecoder.BuildDecodeArguments(config.Magick, "BMP");

        arguments.TakeWhile(a => a != "--").Should().Contain(
            [$"--as={config.Magick.AddressSpaceBytes}", $"--cpu={config.Magick.CpuSeconds}", "--nofile=64"]);
        arguments.SkipWhile(a => a != "--").Skip(1).First().Should().Be("magick");
        arguments.Should().ContainInConsecutiveOrder("-limit", "thread", "1");
        arguments.Should().ContainInConsecutiveOrder("-limit", "memory", config.Magick.MemoryLimit);
        arguments.Should().Contain("BMP:-[0]");
        arguments[^1].Should().Be("png:-");
    }

    [Fact]
    public async Task IsAvailableAsync_disabled_returnsFalse()
    {
        var sut = CreateSut(CreateConfig(c => c.Enabled = false));

        (await sut.IsAvailableAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task IsAvailableAsync_missingBinary_returnsFalse()
    {
        var sut = CreateSut(CreateConfig(c => c.BinaryPath = "definitely-not-imagemagick"));

        (await sut.IsAvailableAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task DecodeToPngAsync_inputOverLimit_throwsBeforeStarting()
    {
        var sut = CreateSut(CreateConfig(c => c.MaxInputBytes = 10));

        var act = () => sut.DecodeToPngAsync(new MemoryStream(Bitmap(8, 8)), "BMP",
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ImageMagickException>();
    }

    [Fact]
    public async Task DecodeToPngAsync_bitmap_returnsPng()
    {
        Assert.SkipUnless(MagickBinary is not null, "ImageMagick is not installed.");
        var sut = CreateSut(CreateConfig());

        var png = await sut.DecodeToPngAsync(new MemoryStream(Bitmap(40, 20)), "BMP",
            TestContext.Current.CancellationToken);

        using var image = Image.NewFromBuffer(png);
        image.Width.Should().Be(40);
        image.Height.Should().Be(20);
    }

    [Fact]
    public async Task DecodeToPngAsync_outputOverLimit_throws()
    {
        Assert.SkipUnless(MagickBinary is not null, "ImageMagick is not installed.");
        var sut = CreateSut(CreateConfig(c => c.MaxOutputBytes = 16));

        var act = () => sut.DecodeToPngAsync(new MemoryStream(Bitmap(40, 20)), "BMP",
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ImageMagickException>();
    }

    [Fact]
    public async Task DecodeToPngAsync_corruptInput_throwsWithProcessError()
    {
        Assert.SkipUnless(MagickBinary is not null, "ImageMagick is not installed.");
        var sut = CreateSut(CreateConfig());

        var act = () => sut.DecodeToPngAsync(new MemoryStream("BM garbage"u8.ToArray()), "BMP",
            TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ImageMagickException>()).Which.ExitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task ImagePreviewService_bitmapViaFallback_producesWebpArtifacts()
    {
        Assert.SkipUnless(MagickBinary is not null, "ImageMagick is not installed.");
        var options = Options.Create(CreateConfig());
        var sut = new ImagePreviewService(
            options,
            new MagickCliDecoder(options, NullLogger<MagickCliDecoder>.Instance),
            NullLogger<ImagePreviewService>.Instance);

        await using var result = await sut.GenerateAsync(new MemoryStream(Bitmap(40, 20)), "image/bmp",
            TestContext.Current.CancellationToken);

        result.Preview.ContentType.Should().Be("image/webp");
        result.Thumbnail.ContentType.Should().Be("image/webp");
    }
}
