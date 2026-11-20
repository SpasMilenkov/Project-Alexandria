using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Preview.Documents.BodyText;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alexandria.Tests.Unit.Documents.BodyText;

public class OpenDocumentTextReaderTests
{
    private static string Content(string body) => $"""
                                                   <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                                                       xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
                                                       xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"
                                                       xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
                                                       xmlns:dc="http://purl.org/dc/elements/1.1/">
                                                     <office:body>{body}</office:body>
                                                   </office:document-content>
                                                   """;

    [Fact]
    public void Text_readsHeadingsParagraphsAndSpacingMarkers()
    {
        using var package = PackageBuilder.Build(("content.xml", Content("""
                                                                         <office:text>
                                                                           <text:h>Trip notes</text:h>
                                                                           <text:p>Hotel<text:s/>in<text:tab/><text:span>Paris</text:span><text:line-break/>second line</text:p>
                                                                         </office:text>
                                                                         """)));

        Read(package).Should().Be("Trip notes\nHotel in Paris\nsecond line");
    }

    [Fact]
    public void Text_skipsCommentsTrackedDeletionsAndNoteNumbers()
    {
        using var package = PackageBuilder.Build(("content.xml", Content("""
                                                                         <office:text>
                                                                           <text:tracked-changes><text:changed-region><text:deletion><text:p>deleted words</text:p></text:deletion></text:changed-region></text:tracked-changes>
                                                                           <text:p>kept<office:annotation><dc:creator>Ben</dc:creator><text:p>reviewer remark</text:p></office:annotation>
                                                                             <text:note><text:note-citation>1</text:note-citation><text:note-body><text:p>footnote text</text:p></text:note-body></text:note>
                                                                           </text:p>
                                                                         </office:text>
                                                                         """)));

        // The footnote sits inside its paragraph, so its text joins the sentence it annotates.
        Read(package).Should().Be("kept footnote text");
    }

    [Fact]
    public void Presentation_endsAPagePerSlide()
    {
        using var package = PackageBuilder.Build(("content.xml", Content("""
                                                                         <office:presentation>
                                                                           <draw:page><draw:frame><draw:text-box><text:p>first slide</text:p></draw:text-box></draw:frame></draw:page>
                                                                           <draw:page/>
                                                                           <draw:page><draw:frame><draw:text-box><text:p>third slide</text:p></draw:text-box></draw:frame></draw:page>
                                                                         </office:presentation>
                                                                         """)));

        Read(package).Should().Be("first slide\f\fthird slide");
    }

    [Fact]
    public void Spreadsheet_endsAPagePerSheet()
    {
        using var package = PackageBuilder.Build(("content.xml", Content("""
                                                                         <office:spreadsheet>
                                                                           <table:table><table:table-row><table:table-cell><text:p>Budget</text:p></table:table-cell></table:table-row></table:table>
                                                                           <table:table><table:table-row><table:table-cell><text:p>412</text:p></table:table-cell></table:table-row></table:table>
                                                                         </office:spreadsheet>
                                                                         """)));

        Read(package, isSpreadsheet: true).Should().Be("Budget\f412");
    }

    [Fact]
    public void Spreadsheet_putsEachRowOnOneLine_likeXlsx()
    {
        using var package = PackageBuilder.Build(("content.xml", Content("""
                                                                         <office:spreadsheet><table:table>
                                                                           <table:table-row>
                                                                             <table:table-cell><text:p>Hotel</text:p><text:p>Paris</text:p></table:table-cell>
                                                                             <table:table-cell><text:p>412.5</text:p></table:table-cell>
                                                                             <table:table-cell table:number-columns-repeated="1000"/>
                                                                           </table:table-row>
                                                                           <table:table-row><table:table-cell><text:p>Train</text:p></table:table-cell></table:table-row>
                                                                         </table:table></office:spreadsheet>
                                                                         """)));

        Read(package, isSpreadsheet: true).Should().Be("Hotel Paris 412.5\nTrain");
    }

    [Fact]
    public void Encrypted_isReportedWithoutReadingContent()
    {
        using var package = PackageBuilder.Build(
            ("META-INF/manifest.xml", """
                                      <manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0">
                                        <manifest:file-entry manifest:full-path="content.xml"><manifest:encryption-data/></manifest:file-entry>
                                      </manifest:manifest>
                                      """),
            ("content.xml", "scrambled"));
        var collector = new TextCollector();

        var status = OpenDocumentTextReader.Read(package, collector, NullLogger.Instance);

        status.Should().Be(DocumentTextStatus.PasswordProtected);
    }

    [Fact]
    public void MissingContent_throws()
    {
        using var package = PackageBuilder.Build(("meta.xml", "<office:document-meta/>"));

        var read = () => OpenDocumentTextReader.Read(package, new TextCollector(), NullLogger.Instance);

        read.Should().Throw<InvalidDataException>();
    }

    private static string Read(Stream package, bool isSpreadsheet = false)
    {
        var collector = new TextCollector();

        var status = OpenDocumentTextReader.Read(package, collector, NullLogger.Instance, isSpreadsheet);

        status.Should().BeNull();
        return collector.Build();
    }
}