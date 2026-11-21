using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Preview.Documents.BodyText;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alexandria.Tests.Unit.Documents.BodyText;

public class OfficeOpenXmlTextReaderTests
{
    private const string WordNs = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string DrawingNs = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private const string SheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static string WordDocument(string body) => $"""
                                                        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                                                        <w:document xmlns:w="{WordNs}" xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                                                            xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing">
                                                          <w:body>{body}</w:body>
                                                        </w:document>
                                                        """;

    private static string Slide(string paragraphs) => $"""
                                                       <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="{DrawingNs}">
                                                         <p:cSld><p:spTree><p:sp><p:txBody>{paragraphs}</p:txBody></p:sp></p:spTree></p:cSld>
                                                       </p:sld>
                                                       """;

    private static string Sheet(string rows) => $"""
                                                 <worksheet xmlns="{SheetNs}"><sheetData>{rows}</sheetData></worksheet>
                                                 """;

    [Fact]
    public void Wordprocessing_readsParagraphsTabsAndBreaks()
    {
        using var package = PackageBuilder.Build(
            ("_rels/.rels", PackageBuilder.PackageRels),
            ("word/document.xml", WordDocument("""
                                               <w:p><w:r><w:t>Hotel</w:t></w:r><w:r><w:t xml:space="preserve"> </w:t></w:r><w:r><w:t>invoice</w:t></w:r></w:p>
                                               <w:p><w:r><w:t>Total</w:t><w:tab/><w:t>412</w:t><w:br/><w:t>EUR</w:t></w:r></w:p>
                                               """)));

        var text = ReadWord(package);

        text.Should().Be("Hotel invoice\nTotal 412\nEUR");
    }

    [Fact]
    public void Wordprocessing_skipsDeletedTextFieldCodesAndDrawingNumbers()
    {
        using var package = PackageBuilder.Build(("word/document.xml", WordDocument("""
            <w:p>
              <w:r><w:t>kept</w:t></w:r>
              <w:del><w:r><w:delText>removed</w:delText></w:r></w:del>
              <w:r><w:instrText>PAGE \* MERGEFORMAT</w:instrText></w:r>
              <w:r><w:drawing><wp:anchor><wp:positionH><wp:posOffset>914400</wp:posOffset></wp:positionH></wp:anchor></w:drawing></w:r>
            </w:p>
            """)));

        ReadWord(package).Should().Be("kept");
    }

    [Fact]
    public void Wordprocessing_readsTextBoxesOnce_skippingTheFallbackCopy()
    {
        using var package = PackageBuilder.Build(("word/document.xml", WordDocument("""
            <w:p><w:r><mc:AlternateContent>
              <mc:Choice Requires="wps"><w:txbxContent><w:p><w:r><w:t>boxed</w:t></w:r></w:p></w:txbxContent></mc:Choice>
              <mc:Fallback><w:txbxContent><w:p><w:r><w:t>boxed</w:t></w:r></w:p></w:txbxContent></mc:Fallback>
            </mc:AlternateContent></w:r></w:p>
            """)));

        ReadWord(package).Should().Be("boxed");
    }

    [Fact]
    public void Wordprocessing_addsFootnotesHeadersAndFooters()
    {
        using var package = PackageBuilder.Build(
            ("word/document.xml", WordDocument("<w:p><w:r><w:t>body</w:t></w:r></w:p>")),
            ("word/footnotes.xml",
                $"""<w:footnotes xmlns:w="{WordNs}"><w:footnote><w:p><w:r><w:t>note</w:t></w:r></w:p></w:footnote></w:footnotes>"""),
            ("word/header1.xml",
                $"""<w:hdr xmlns:w="{WordNs}"><w:p><w:r><w:t>Acme letterhead</w:t></w:r></w:p></w:hdr>"""));

        ReadWord(package).Should().Be("body\nnote\nAcme letterhead");
    }

    [Fact]
    public void Wordprocessing_brokenFootnotes_keepTheBody()
    {
        using var package = PackageBuilder.Build(
            ("word/document.xml", WordDocument("<w:p><w:r><w:t>body</w:t></w:r></w:p>")),
            ("word/footnotes.xml", "<w:footnotes"));

        ReadWord(package).Should().Be("body");
    }

    [Fact]
    public void Wordprocessing_brokenBody_throws()
    {
        using var package = PackageBuilder.Build(("word/document.xml", "<w:document"));

        var read = () => ReadWord(package);

        read.Should().Throw<XmlException>();
    }

    [Fact]
    public void Wordprocessing_withoutBody_throws()
    {
        using var package = PackageBuilder.Build(("docProps/core.xml", PackageBuilder.CoreProperties));

        var read = () => ReadWord(package);

        read.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Wordprocessing_passwordProtected_isReportedWithoutReading()
    {
        using var source = new MemoryStream([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0]);
        var collector = new TextCollector();

        var status = OfficeOpenXmlTextReader.ReadWordprocessing(source, collector, NullLogger.Instance);

        status.Should().Be(DocumentTextStatus.PasswordProtected);
        collector.Build().Should().BeEmpty();
    }

    [Fact]
    public void Presentation_makesOnePagePerSlide_inNumericOrder_withNotes()
    {
        using var package = PackageBuilder.Build(
            ("ppt/slides/slide10.xml", Slide("<a:p><a:r><a:t>tenth</a:t></a:r></a:p>")),
            ("ppt/slides/slide2.xml", Slide("<a:p><a:r><a:t>second</a:t></a:r></a:p>")),
            ("ppt/slides/slide1.xml",
                Slide("<a:p><a:r><a:t>Title</a:t></a:r><a:br/><a:r><a:t>subtitle</a:t></a:r></a:p>")),
            ("ppt/slides/_rels/slide1.xml.rels", """
                                                 <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                                                   <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/notesSlide" Target="../notesSlides/notesSlide7.xml"/>
                                                 </Relationships>
                                                 """),
            ("ppt/notesSlides/notesSlide7.xml", Slide("<a:p><a:r><a:t>speaker note</a:t></a:r></a:p>")));

        var text = ReadPresentation(package);

        text.Should().Be("Title\nsubtitle\nspeaker note\fsecond\ftenth");
    }

    [Fact]
    public void Presentation_skipsSlideNumberFields()
    {
        using var package = PackageBuilder.Build(("ppt/slides/slide1.xml", Slide("""
            <a:p><a:r><a:t>Agenda</a:t></a:r></a:p>
            <a:p><a:fld type="slidenum"><a:t>1</a:t></a:fld></a:p>
            """)));

        ReadPresentation(package).Should().Be("Agenda");
    }

    [Fact]
    public void Spreadsheet_readsSharedInlineAndNumberCells_skippingBooleansAndErrors()
    {
        using var package = PackageBuilder.Build(
            ("xl/sharedStrings.xml", $"""
                                      <sst xmlns="{SheetNs}">
                                        <si><t>Invoice</t></si>
                                        <si><r><t>Paris </t></r><r><t>hotel</t></r><rPh><t>ぱり</t></rPh></si>
                                      </sst>
                                      """),
            ("xl/worksheets/sheet1.xml", Sheet("""
                                               <row><c t="s"><v>0</v></c><c><v>412.5</v></c></row>
                                               <row><c t="s"><v>1</v></c><c t="inlineStr"><is><t>night</t></is></c><c t="b"><v>1</v></c><c t="e"><v>#DIV/0!</v></c></row>
                                               """)));

        var text = ReadSpreadsheet(package);

        text.Should().Be("Invoice 412.5\nParis hotel night");
    }

    [Fact]
    public void Spreadsheet_makesOnePagePerSheet_inNumericOrder()
    {
        using var package = PackageBuilder.Build(
            ("xl/worksheets/sheet2.xml", Sheet("""<row><c t="inlineStr"><is><t>second</t></is></c></row>""")),
            ("xl/worksheets/sheet1.xml", Sheet("""<row><c t="inlineStr"><is><t>first</t></is></c></row>""")));

        ReadSpreadsheet(package).Should().Be("first\fsecond");
    }

    [Fact]
    public void Spreadsheet_ignoresFormulasAndOutOfRangeStringIndexes()
    {
        using var package = PackageBuilder.Build(("xl/worksheets/sheet1.xml", Sheet("""
            <row><c><f>SUM(A1:A2)</f><v>7</v></c><c t="s"><v>99</v></c><c t="str"><f>A1</f><v>computed</v></c></row>
            """)));

        ReadSpreadsheet(package).Should().Be("7 computed");
    }

    [Fact]
    public void BodyPart_pastItsSizeLimit_keepsTheTextReadSoFar()
    {
        var paragraphs =
            string.Concat(Enumerable.Range(0, 2000).Select(i => $"<w:p><w:r><w:t>word{i}</w:t></w:r></w:p>"));
        using var package = PackageBuilder.Build(("word/document.xml", WordDocument(paragraphs)));
        using var archive = new ZipArchive(package, ZipArchiveMode.Read);
        var collector = new TextCollector();

        TextPackagePart.Read(
            archive.GetEntry("word/document.xml")!,
            new XmlTextRules { TextContainers = new HashSet<XName> { XName.Get("t", WordNs) } },
            collector,
            isBody: true,
            NullLogger.Instance,
            maxBytes: 4096);

        collector.IsTruncated.Should().BeTrue();
        collector.Build().Should().StartWith("word0");
    }

    [Theory]
    [InlineData("ppt/slides", "../notesSlides/notesSlide1.xml", "ppt/notesSlides/notesSlide1.xml")]
    [InlineData("word", "media/image.png", "word/media/image.png")]
    [InlineData("OEBPS", "./Text/ch1.xhtml", "OEBPS/Text/ch1.xhtml")]
    [InlineData("", "chapter.xhtml", "chapter.xhtml")]
    [InlineData("ppt/slides", "/ppt/media/a.png", "ppt/media/a.png")]
    [InlineData("a", "../../escape.xml", "escape.xml")]
    public void ResolveRelative_followsPackageLinks(string directory, string target, string expected)
    {
        TextPackagePart.ResolveRelative(directory, target).Should().Be(expected);
    }

    private static string ReadWord(Stream package) => Read(package, OfficeOpenXmlTextReader.ReadWordprocessing);

    private static string ReadPresentation(Stream package) => Read(package, OfficeOpenXmlTextReader.ReadPresentation);

    private static string ReadSpreadsheet(Stream package) => Read(package, OfficeOpenXmlTextReader.ReadSpreadsheet);

    private static string Read(
        Stream package,
        Func<Stream, TextCollector, ILogger, DocumentTextStatus?> reader)
    {
        var collector = new TextCollector();

        var status = reader(package, collector, NullLogger.Instance);

        status.Should().BeNull();
        return collector.Build();
    }
}