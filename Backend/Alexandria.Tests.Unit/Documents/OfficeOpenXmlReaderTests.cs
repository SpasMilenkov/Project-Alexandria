using Alexandria.Services.Preview.Documents.Metadata;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alexandria.Tests.Unit.Documents;

public class OfficeOpenXmlReaderTests
{
    private static DocumentFields Read(Stream source) => OfficeOpenXmlReader.Read(source, NullLogger.Instance);

    [Fact]
    public void Read_wordDocument_collectsCoreAndExtendedProperties()
    {
        using var package = PackageBuilder.Build(
            ("_rels/.rels", PackageBuilder.PackageRels),
            ("docProps/core.xml", PackageBuilder.CoreProperties),
            ("docProps/app.xml", PackageBuilder.ExtendedProperties));

        var fields = Read(package).Values;

        fields["core:title"].Should().Be("Quarterly report");
        fields["core:creator"].Should().Be("Ana Doe");
        fields["core:lastModifiedBy"].Should().Be("Ben Roe");
        fields["core:created"].Should().Be("2024-03-01T10:20:00Z");
        fields["app:Pages"].Should().Be("3");
        fields["app:Company"].Should().Be("Acme");
        fields["app:TotalTime"].Should().Be("42");
    }

    [Fact]
    public void Read_extendedPartVectors_areSkippedRatherThanFlattened()
    {
        using var package = PackageBuilder.Build(
            ("_rels/.rels", PackageBuilder.PackageRels),
            ("docProps/app.xml", PackageBuilder.ExtendedProperties));

        var fields = Read(package).Values;

        fields.Should().NotContainKey("app:TitlesOfParts");
    }

    [Fact]
    public void Read_relationshipsPointElsewhere_followsTheRelationships()
    {
        const string rels = """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officedocument/2006/relationships/metadata/core-properties" Target="/meta/core1.xml"/>
            </Relationships>
            """;
        using var package = PackageBuilder.Build(
            ("_rels/.rels", rels),
            ("meta/core1.xml", PackageBuilder.CoreProperties));

        var fields = Read(package).Values;

        fields["core:title"].Should().Be("Quarterly report");
    }

    [Fact]
    public void Read_presentationWithoutExtendedPart_countsSlideParts()
    {
        using var package = PackageBuilder.Build(
            ("ppt/slides/slide1.xml", "<p:sld xmlns:p=\"urn:p\"/>"),
            ("ppt/slides/slide2.xml", "<p:sld xmlns:p=\"urn:p\"/>"),
            ("ppt/slides/_rels/slide1.xml.rels", "<Relationships/>"));

        var fields = Read(package).Values;

        fields["package:slides"].Should().Be("2");
    }

    [Fact]
    public void Read_workbook_countsSheetParts()
    {
        using var package = PackageBuilder.Build(
            ("xl/worksheets/sheet1.xml", "<worksheet/>"),
            ("xl/worksheets/sheet2.xml", "<worksheet/>"),
            ("xl/worksheets/sheet3.xml", "<worksheet/>"));

        var fields = Read(package).Values;

        fields["package:sheets"].Should().Be("3");
    }

    [Fact]
    public void Read_compoundFile_isPasswordProtected()
    {
        byte[] signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0];
        using var source = new MemoryStream(signature);

        var fields = Read(source);

        fields.IsPasswordProtected.Should().BeTrue();
        fields.Values.Should().BeEmpty();
    }

    [Fact]
    public void Read_partWithDocumentType_isSkippedButOtherPartsStillRead()
    {
        const string hostile = """
            <?xml version="1.0"?>
            <!DOCTYPE lolz [<!ENTITY lol "lol">]>
            <cp:coreProperties xmlns:cp="urn:cp" xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:title>&lol;</dc:title></cp:coreProperties>
            """;
        using var package = PackageBuilder.Build(
            ("_rels/.rels", PackageBuilder.PackageRels),
            ("docProps/core.xml", hostile),
            ("docProps/app.xml", PackageBuilder.ExtendedProperties));

        var fields = Read(package).Values;

        fields.Should().NotContainKey("core:title");
        fields["app:Pages"].Should().Be("3");
    }

    [Fact]
    public void Read_partInflatingPastTheLimit_isSkipped()
    {
        var padding = new string(' ', (int)PackagePart.MaxPropertyPartBytes + 1);
        var oversized = $"<Properties><Pages>3</Pages>{padding}</Properties>";
        using var package = PackageBuilder.Build(
            ("_rels/.rels", PackageBuilder.PackageRels),
            ("docProps/core.xml", PackageBuilder.CoreProperties),
            ("docProps/app.xml", oversized));

        var fields = Read(package).Values;

        fields.Should().NotContainKey("app:Pages");
        fields["core:title"].Should().Be("Quarterly report");
    }

    [Fact]
    public void Read_notAZip_throws()
    {
        using var source = new MemoryStream("plain text, not a package"u8.ToArray());

        var act = () => Read(source);

        act.Should().Throw<InvalidDataException>();
    }
}
