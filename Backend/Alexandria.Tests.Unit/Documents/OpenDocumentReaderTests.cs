using Alexandria.Services.Preview.Documents.Metadata;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alexandria.Tests.Unit.Documents;

public class OpenDocumentReaderTests
{
    private const string Meta = """
        <?xml version="1.0" encoding="UTF-8"?>
        <office:document-meta xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
            xmlns:meta="urn:oasis:names:tc:opendocument:xmlns:meta:1.0" xmlns:dc="http://purl.org/dc/elements/1.1/">
          <office:meta>
            <meta:creation-date>2026-01-08T19:25:31.437770626</meta:creation-date>
            <dc:date>2026-01-08T20:25:53.981886302</dc:date>
            <meta:editing-duration>PT7M11S</meta:editing-duration>
            <meta:generator>LibreOffice/24.2.7.2$Linux_X86_64 LibreOffice_project/420$Build-2</meta:generator>
            <meta:initial-creator>Ana Doe</meta:initial-creator>
            <dc:creator>Ben Roe</dc:creator>
            <meta:keyword>graphs</meta:keyword>
            <meta:keyword>euler</meta:keyword>
            <meta:user-defined meta:name="Client">Acme</meta:user-defined>
            <meta:document-statistic meta:table-count="0" meta:page-count="3" meta:word-count="224"/>
          </office:meta>
        </office:document-meta>
        """;

    private const string Content = """
        <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
            xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0">
          <office:body><office:presentation>
            <draw:page draw:name="one"/><draw:page draw:name="two"/><draw:page draw:name="three"/>
          </office:presentation></office:body>
        </office:document-content>
        """;

    private static DocumentFields Read(Stream source, bool isPresentation = false)
        => OpenDocumentReader.Read(source, isPresentation, NullLogger.Instance);

    [Fact]
    public void Read_textDocument_collectsMetaElementsAndStatistics()
    {
        using var package = PackageBuilder.Build(("meta.xml", Meta));

        var fields = Read(package).Values;

        fields["odf:initial-creator"].Should().Be("Ana Doe");
        fields["odf:creator"].Should().Be("Ben Roe");
        fields["odf:creation-date"].Should().Be("2026-01-08T19:25:31.437770626");
        fields["odf:editing-duration"].Should().Be("PT7M11S");
        fields["odf-stat:page-count"].Should().Be("3");
        fields["odf-stat:word-count"].Should().Be("224");
    }

    [Fact]
    public void Read_repeatedKeywords_areJoined()
    {
        using var package = PackageBuilder.Build(("meta.xml", Meta));

        var fields = Read(package).Values;

        fields["odf:keyword"].Should().Be("graphs; euler");
    }

    [Fact]
    public void Read_userDefinedProperty_isKeyedByItsName()
    {
        using var package = PackageBuilder.Build(("meta.xml", Meta));

        var fields = Read(package).Values;

        fields["odf-user:Client"].Should().Be("Acme");
    }

    [Fact]
    public void Read_presentation_countsSlidesInTheContentPart()
    {
        using var package = PackageBuilder.Build(("meta.xml", Meta), ("content.xml", Content));

        var fields = Read(package, isPresentation: true).Values;

        fields["package:pages"].Should().Be("3");
    }

    [Fact]
    public void Read_textDocument_leavesTheContentPartAlone()
    {
        using var package = PackageBuilder.Build(("meta.xml", Meta), ("content.xml", Content));

        var fields = Read(package).Values;

        fields.Should().NotContainKey("package:pages");
    }

    [Fact]
    public void Read_encryptedPackage_isPasswordProtected()
    {
        const string manifest = """
            <manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0">
              <manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml">
                <manifest:encryption-data manifest:checksum-type="SHA1/1K"/>
              </manifest:file-entry>
            </manifest:manifest>
            """;
        using var package = PackageBuilder.Build(("META-INF/manifest.xml", manifest), ("meta.xml", Meta));

        var fields = Read(package);

        fields.IsPasswordProtected.Should().BeTrue();
        fields.Values.Should().BeEmpty();
    }
}
