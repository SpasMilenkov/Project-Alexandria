using System.Text;
using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Services.Preview;
using Alexandria.Services.Preview.Images;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetVips;
using NSubstitute;

namespace Alexandria.Tests.Unit.PreviewJobs;

/// <summary>
/// Runs the real libvips header read over a JPEG with hand-built EXIF, so these fail if a
/// libvips upgrade changes how it reports fields.
/// </summary>
public class CaptureMetadataServiceTests
{
    private const ushort TypeAscii = 2;
    private const ushort TypeShort = 3;
    private const ushort TypeLong = 4;
    private const ushort TypeRational = 5;

    private readonly ICaptureMetadataRepository _repository = Substitute.For<ICaptureMetadataRepository>();
    private readonly Guid _versionId = Guid.NewGuid();

    private CaptureMetadataService CreateSut()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.CaptureMetadata.Returns(_repository);

        return new CaptureMetadataService(
            Options.Create(new ImagePreviewConfig()),
            unitOfWork,
            NullLogger<CaptureMetadataService>.Instance);
    }

    private async Task<CaptureMetadata> ExtractAsync(byte[] image)
    {
        CaptureMetadata? stored = null;
        await _repository.UpsertAsync(
            Arg.Do<CaptureMetadata>(m => stored = m),
            Arg.Any<CancellationToken>());

        await using var source = new MemoryStream(image);
        await CreateSut().ExtractAndStoreAsync(_versionId, source, TestContext.Current.CancellationToken);

        stored.Should().NotBeNull();
        return stored!;
    }

    [Fact]
    public async Task tagged_jpeg_fills_every_column_from_real_libvips_output()
    {
        var entity = await ExtractAsync(TaggedJpeg(orientation: 1));

        entity.FileVersionId.Should().Be(_versionId);
        entity.Width.Should().Be(64);
        entity.Height.Should().Be(48);
        entity.CameraMake.Should().Be("Canon");
        entity.CameraModel.Should().Be("EOS R6 II");
        entity.LensModel.Should().Be("EF 24-70mm (f/2.8)");
        entity.Software.Should().Be("Lr 9.0");
        entity.CapturedAt.Should().Be(new DateTime(2026, 8, 14, 15, 22, 31, DateTimeKind.Utc));
        entity.CapturedOffsetMinutes.Should().Be(180);
        entity.Latitude.Should().BeApproximately(42.684722, 0.00001);
        entity.Longitude.Should().BeApproximately(23.322222, 0.00001);
        entity.FNumber.Should().Be(1.8);
        entity.ExposureSeconds.Should().Be(0.004);
        entity.FocalLengthMm.Should().Be(50);
        entity.Iso.Should().Be(200);
    }

    [Fact]
    public async Task raw_json_is_valid_for_jsonb_and_leaves_out_binary_blobs()
    {
        var entity = await ExtractAsync(TaggedJpeg(orientation: 1));

        using var document = JsonDocument.Parse(entity.RawJson);
        var root = document.RootElement;

        root.GetProperty("exif-ifd0-Make").GetString().Should().Be("Canon");
        root.GetProperty("vips-loader").GetString().Should().NotBeNullOrEmpty();
        root.TryGetProperty("exif-data", out _).Should().BeFalse();
        entity.RawJson.Should().NotContain("\\u0000");
    }

    [Fact]
    public async Task rotated_jpeg_stores_its_display_dimensions()
    {
        var entity = await ExtractAsync(TaggedJpeg(orientation: 6));

        entity.Width.Should().Be(48);
        entity.Height.Should().Be(64);
    }

    [Fact]
    public async Task jpeg_without_exif_still_gets_a_row_with_dimensions()
    {
        var entity = await ExtractAsync(PlainJpeg());

        entity.Width.Should().Be(64);
        entity.CapturedAt.Should().BeNull();
        entity.Latitude.Should().BeNull();
    }

    [Fact]
    public async Task format_with_no_loader_gets_an_empty_row_instead_of_failing()
    {
        var entity = await ExtractAsync(Encoding.ASCII.GetBytes("BM this is not something libvips can load"));

        entity.Width.Should().BeNull();
        entity.RawJson.Should().Be("{}");
    }

    [Fact]
    public async Task truncated_header_throws_so_the_version_is_retried_rather_than_marked_empty()
    {
        var truncated = PlainJpeg()[..6];
        await using var source = new MemoryStream(truncated);

        var act = () => CreateSut().ExtractAndStoreAsync(_versionId, source, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<VipsException>();
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<CaptureMetadata>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Apple (Apple, ASCII, 6 components, 6 bytes)", "Apple")]
    [InlineData("EF 24-70mm (f/2.8) (EF 24-70mm (f/2.8), ASCII, 19 components, 19 bytes)", "EF 24-70mm (f/2.8)")]
    [InlineData("8/5 (f/1.6, Rational, 1 components, 8 bytes)", "8/5")]
    [InlineData("6 (Internal error (unknown value 6), Short, 1 components, 2 bytes)", "6")]
    [InlineData("42/1 41/1 5/1 (42, 41,  5, Rational, 3 components, 24 bytes)", "42/1 41/1 5/1")]
    [InlineData("not in the libvips format", "not in the libvips format")]
    public void raw_exif_value_is_recovered_from_the_formatted_string(string formatted, string expected)
    {
        CaptureHeaderReader.RawExifValue(formatted).Should().Be(expected);
    }

    private static byte[] PlainJpeg()
    {
        using var black = Image.Black(64, 48, 3);
        using var image = black.Cast(Enums.BandFormat.Uchar);
        return image.WriteToBuffer(".jpg");
    }

    private static byte[] TaggedJpeg(ushort orientation)
    {
        var plain = PlainJpeg();
        var exif = BuildExifSegment(orientation);

        using var output = new MemoryStream();
        output.Write(plain, 0, 2);
        output.Write(exif);
        output.Write(plain, 2, plain.Length - 2);

        return output.ToArray();
    }

    private static byte[] BuildExifSegment(ushort orientation)
    {
        var gps = new List<IfdEntry>
        {
            Ascii(0x0001, "N"),
            Rational(0x0002, (42, 1), (41, 1), (5, 1)),
            Ascii(0x0003, "E"),
            Rational(0x0004, (23, 1), (19, 1), (20, 1))
        };

        var exif = new List<IfdEntry>
        {
            Rational(0x829A, (1, 250)),
            Rational(0x829D, (18, 10)),
            Short(0x8827, 200),
            Ascii(0x9003, "2026:08:14 18:22:31"),
            Ascii(0x9011, "+03:00"),
            Rational(0x920A, (50, 1)),
            Ascii(0xA434, "EF 24-70mm (f/2.8)")
        };

        var root = new List<IfdEntry>
        {
            Ascii(0x010F, "Canon"),
            Ascii(0x0110, "EOS R6 II"),
            Short(0x0112, orientation),
            Ascii(0x0131, "Lr 9.0"),
            Long(0x8769, 0),
            Long(0x8825, 0)
        };

        const int rootOffset = 8;
        var exifOffset = rootOffset + WriteIfd(root, rootOffset).Length;
        var gpsOffset = exifOffset + WriteIfd(exif, exifOffset).Length;

        root[4] = Long(0x8769, (uint)exifOffset);
        root[5] = Long(0x8825, (uint)gpsOffset);

        using var tiff = new MemoryStream();
        tiff.Write("II*\0"u8);
        tiff.Write(BitConverter.GetBytes((uint)rootOffset));
        tiff.Write(WriteIfd(root, rootOffset));
        tiff.Write(WriteIfd(exif, exifOffset));
        tiff.Write(WriteIfd(gps, gpsOffset));

        var body = "Exif\0\0"u8.ToArray().Concat(tiff.ToArray()).ToArray();
        var length = body.Length + 2;

        return [0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. body];
    }

    private static byte[] WriteIfd(List<IfdEntry> entries, int offset)
    {
        var sorted = entries.OrderBy(e => e.Tag).ToList();
        var dataOffset = offset + 2 + sorted.Count * 12 + 4;

        using var head = new MemoryStream();
        using var tail = new MemoryStream();
        head.Write(BitConverter.GetBytes((ushort)sorted.Count));

        foreach (var entry in sorted)
        {
            head.Write(BitConverter.GetBytes(entry.Tag));
            head.Write(BitConverter.GetBytes(entry.Type));
            head.Write(BitConverter.GetBytes(entry.Count));

            if (entry.Value.Length <= 4)
            {
                head.Write(entry.Value);
                head.Write(new byte[4 - entry.Value.Length]);
                continue;
            }

            head.Write(BitConverter.GetBytes((uint)(dataOffset + tail.Length)));
            tail.Write(entry.Value);
            if (tail.Length % 2 != 0)
                tail.WriteByte(0);
        }

        head.Write(BitConverter.GetBytes(0u));
        head.Write(tail.ToArray());

        return head.ToArray();
    }

    private static IfdEntry Ascii(ushort tag, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value + "\0");
        return new IfdEntry(tag, TypeAscii, (uint)bytes.Length, bytes);
    }

    private static IfdEntry Short(ushort tag, ushort value) =>
        new(tag, TypeShort, 1, BitConverter.GetBytes(value));

    private static IfdEntry Long(ushort tag, uint value) =>
        new(tag, TypeLong, 1, BitConverter.GetBytes(value));

    private static IfdEntry Rational(ushort tag, params (uint Numerator, uint Denominator)[] values)
    {
        var bytes = values
            .SelectMany(v => BitConverter.GetBytes(v.Numerator).Concat(BitConverter.GetBytes(v.Denominator)))
            .ToArray();

        return new IfdEntry(tag, TypeRational, (uint)values.Length, bytes);
    }

    private sealed record IfdEntry(ushort Tag, ushort Type, uint Count, byte[] Value);
}
