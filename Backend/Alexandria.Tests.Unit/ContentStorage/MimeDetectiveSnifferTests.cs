using System.IO.Compression;
using System.Text;
using Alexandria.Services.Storage.Content.Mime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Alexandria.Tests.Unit.ContentStorage;

public class MimeDetectiveSnifferTests
{
    [Fact]
    public void S13_a_fault_in_the_inspector_is_reported_as_no_match()
    {
        var sniffer = new MimeDetectiveSniffer(
            NullLogger<MimeDetectiveSniffer>.Instance,
            _ => throw new InvalidOperationException("forced fault"));

        Assert.Null(sniffer.Detect([1, 2, 3]));
    }

    [Fact]
    public void Detect_returns_null_for_an_empty_header_without_calling_the_inspector()
    {
        var sniffer = new MimeDetectiveSniffer(
            NullLogger<MimeDetectiveSniffer>.Instance,
            _ => throw new InvalidOperationException("must not be called"));

        Assert.Null(sniffer.Detect([]));
    }

    [Fact]
    public void Resolver_still_resolves_from_the_extension_when_the_sniffer_faults()
    {
        var sniffer = new MimeDetectiveSniffer(
            NullLogger<MimeDetectiveSniffer>.Instance,
            _ => throw new InvalidOperationException("forced fault"));
        var resolver = new MimeTypeResolver(sniffer);

        var result = resolver.Resolve("report.pdf", [1, 2, 3], "application/octet-stream");

        Assert.Equal("application/pdf", result.MimeType);
        Assert.Equal(MimeSource.Extension, result.Source);
    }

    public static IEnumerable<object[]> CanaryCases() =>
    [
        ["S01", "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52 }],
        ["S02", "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 }],
        ["S03", "image/gif", "GIF89a"u8.ToArray()],
        ["S04", "application/pdf", "%PDF-1.7\n"u8.ToArray()],
        ["S05", "application/zip", MakeZip("hello.txt", "hello")],
        ["S06", "application/gzip", new byte[] { 0x1F, 0x8B, 0x08, 0x00 }],
        ["S07", "application/x-7z-compressed", new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }],
        ["S08", "audio/flac", "fLaC"u8.ToArray()],
        ["S09", "application/ogg", "OggS"u8.ToArray()]
    ];

    [Theory]
    [MemberData(nameof(CanaryCases))]
    public void S01_to_S09_default_definitions_detect_binary_canaries(string id, string expected, byte[] header)
    {
        var sniffer = MakeSniffer();

        Assert.Equal(expected, sniffer.Detect(header));
    }

    [Fact]
    public void S10_an_m4a_container_resolves_to_audio_even_when_sniffed_as_iso_bmff()
    {
        var header = new byte[]
        {
            0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p',
            (byte)'M', (byte)'4', (byte)'A', (byte)' ', 0, 0, 0, 0,
            (byte)'M', (byte)'4', (byte)'A', (byte)' '
        };
        var sniffer = MakeSniffer();
        var sniffed = sniffer.Detect(header);

        Assert.NotNull(sniffed);
        Assert.True(sniffed.StartsWith("audio/", StringComparison.Ordinal) ||
                    sniffed.StartsWith("video/", StringComparison.Ordinal));

        var resolver = new MimeTypeResolver(sniffer);
        var result = resolver.Resolve("song.m4a", header, "application/octet-stream");

        Assert.Equal("audio/mp4", result.MimeType);
        Assert.Equal(MimeSource.Extension, result.Source);
    }

    [Fact]
    public void S11_a_docx_named_file_resolves_to_the_openxml_type()
    {
        var header = MakeZip(
            "[Content_Types].xml",
            "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\" />");
        var resolver = new MimeTypeResolver(MakeSniffer());

        var result = resolver.Resolve("report.docx", header, "application/octet-stream");

        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.MimeType);
        Assert.Equal(MimeSource.Extension, result.Source);
    }

    [Theory]
    [InlineData("plain text\n")]
    [InlineData("{\"a\":1}\n")]
    public void S12_text_and_json_do_not_throw(string content)
    {
        var sniffer = MakeSniffer();

        var exception = Record.Exception(() => sniffer.Detect(Encoding.UTF8.GetBytes(content)));

        Assert.Null(exception);
    }

    [Fact]
    public void S12_an_empty_header_does_not_throw()
    {
        var sniffer = MakeSniffer();

        var exception = Record.Exception(() => sniffer.Detect([]));

        Assert.Null(exception);
    }

    [Fact]
    public void S14_a_dockerfile_resolves_by_filename_not_the_sniffed_text_type()
    {
        var header = Encoding.UTF8.GetBytes(
            "FROM alpine:3.20\nRUN apk add --no-cache curl\nCMD [\"sh\"]\n");
        var resolver = new MimeTypeResolver(MakeSniffer());

        var result = resolver.Resolve("Dockerfile", header, "application/octet-stream");

        Assert.Equal(MimeFileNameMap.Dockerfile, result.MimeType);
        Assert.Equal(MimeSource.FileName, result.Source);
    }

    [Theory]
    [InlineData("Containerfile", "FROM alpine:3.20\n", MimeFileNameMap.Containerfile)]
    [InlineData("Makefile", "all:\n\t@echo ok\n", MimeFileNameMap.Makefile)]
    public void S15_known_extensionless_text_files_resolve_by_filename(
        string fileName, string content, string expected)
    {
        var resolver = new MimeTypeResolver(MakeSniffer());

        var result = resolver.Resolve(fileName, Encoding.UTF8.GetBytes(content), "application/octet-stream");

        Assert.Equal(expected, result.MimeType);
        Assert.Equal(MimeSource.FileName, result.Source);
    }

    [Theory]
    [InlineData("avif", new[] { "mif1", "miaf" })]
    [InlineData("avis", new[] { "msf1", "miaf" })]
    [InlineData("mif1", new[] { "avif", "miaf" })]
    public void S16_avif_brands_sniff_as_avif_instead_of_mp4(string majorBrand, string[] compatibleBrands)
    {
        var sniffer = MakeSniffer();

        Assert.Equal("image/avif", sniffer.Detect(MakeFtyp(majorBrand, compatibleBrands)));
    }

    [Fact]
    public void S17_an_mp4_ftyp_still_sniffs_as_mp4()
    {
        var sniffer = MakeSniffer();

        Assert.Equal("video/mp4", sniffer.Detect(MakeFtyp("isom", ["isom", "mp42"])));
    }

    [Theory]
    [InlineData("photo.avif", MimeSource.SniffedSpecific)]
    [InlineData("photo.mp4", MimeSource.SniffedSpecific)]
    [InlineData("photo", MimeSource.SniffedSpecific)]
    public void S18_avif_bytes_resolve_to_avif_whatever_the_name(string fileName, MimeSource expectedSource)
    {
        var resolver = new MimeTypeResolver(MakeSniffer());

        var result = resolver.Resolve(fileName, MakeFtyp("avif", ["mif1", "miaf"]), "application/octet-stream");

        Assert.Equal("image/avif", result.MimeType);
        Assert.Equal(expectedSource, result.Source);
    }

    [Fact]
    public void S19_compatible_brands_past_the_ftyp_box_are_ignored()
    {
        var header = MakeFtyp("isom", ["mp42"]);
        "avif"u8.CopyTo(header.AsSpan(20));

        Assert.Null(IsoBmffBrands.Detect(header));
    }

    [Fact]
    public void S20_a_box_size_below_the_fixed_fields_only_trusts_the_major_brand()
    {
        var header = MakeFtyp("isom", ["avif"]);
        header[3] = 0;

        Assert.Null(IsoBmffBrands.Detect(header));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    public void S21_a_header_too_short_for_an_ftyp_box_yields_no_brand(int length)
    {
        var header = MakeFtyp("avif", []).AsSpan(0, length).ToArray();

        Assert.Null(IsoBmffBrands.Detect(header));
    }

    private static MimeDetectiveSniffer MakeSniffer()
        => new(NullLogger<MimeDetectiveSniffer>.Instance);

    /// <summary>An ftyp box followed by zero padding, sized from its brand count.</summary>
    private static byte[] MakeFtyp(string majorBrand, string[] compatibleBrands)
    {
        var boxSize = 16 + 4 * compatibleBrands.Length;
        var header = new byte[boxSize + 64];

        header[3] = (byte)boxSize;
        "ftyp"u8.CopyTo(header.AsSpan(4));
        Encoding.ASCII.GetBytes(majorBrand).CopyTo(header.AsSpan(8));

        for (var i = 0; i < compatibleBrands.Length; i++)
            Encoding.ASCII.GetBytes(compatibleBrands[i]).CopyTo(header.AsSpan(16 + 4 * i));

        return header;
    }

    private static byte[] MakeZip(string entryName, string content)
    {
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        return stream.ToArray();
    }
}
