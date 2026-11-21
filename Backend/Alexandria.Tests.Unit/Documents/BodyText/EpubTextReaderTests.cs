using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Preview.Documents.BodyText;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alexandria.Tests.Unit.Documents.BodyText;

public class EpubTextReaderTests
{
    private const string Container = """
                                     <container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0">
                                       <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles>
                                     </container>
                                     """;

    private const string Package = """
                                   <package xmlns="http://www.idpf.org/2007/opf" version="3.0">
                                     <metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:title>Book</dc:title></metadata>
                                     <manifest>
                                       <item id="css" href="style.css" media-type="text/css"/>
                                       <item id="c2" href="Text/chapter%202.xhtml" media-type="application/xhtml+xml"/>
                                       <item id="c1" href="Text/chapter1.xhtml#start" media-type="application/xhtml+xml"/>
                                     </manifest>
                                     <spine><itemref idref="c1"/><itemref idref="css"/><itemref idref="c2"/><itemref idref="c1"/></spine>
                                   </package>
                                   """;

    private static string Chapter(string body) => $$"""
                                                    <?xml version="1.0" encoding="utf-8"?>
                                                    <!DOCTYPE html>
                                                    <html xmlns="http://www.w3.org/1999/xhtml">
                                                      <head><title>Chapter title repeated</title><style>p { color: red; }</style></head>
                                                      <body>{{body}}</body>
                                                    </html>
                                                    """;

    [Fact]
    public void Read_followsTheSpine_onceEach_resolvingEscapedLinks()
    {
        using var package = PackageBuilder.Build(
            ("META-INF/container.xml", Container),
            ("OEBPS/content.opf", Package),
            ("OEBPS/Text/chapter1.xhtml", Chapter("<h1>One</h1><p>It was a dark night.</p>")),
            ("OEBPS/Text/chapter 2.xhtml", Chapter("<p>Morning in Paris.</p>")));

        Read(package).Should().Be("One\nIt was a dark night.\nMorning in Paris.");
    }

    [Fact]
    public void Read_withoutAResolvableSpine_readsEveryChapterInNameOrder()
    {
        using var package = PackageBuilder.Build(
            ("META-INF/container.xml", Container),
            ("OEBPS/content.opf", """<package xmlns="http://www.idpf.org/2007/opf"><manifest/><spine/></package>"""),
            ("OEBPS/b.xhtml", Chapter("<p>second</p>")),
            ("OEBPS/a.xhtml", Chapter("<p>first</p>")));

        Read(package).Should().Be("first\nsecond");
    }

    [Fact]
    public void Read_copyProtected_isReportedWithoutReadingChapters()
    {
        using var package = PackageBuilder.Build(
            ("META-INF/container.xml", Container),
            ("META-INF/rights.xml", "<rights/>"),
            ("OEBPS/content.opf", Package));
        var collector = new TextCollector();

        var status = EpubTextReader.Read(package, collector, NullLogger.Instance);

        status.Should().Be(DocumentTextStatus.CopyProtected);
        collector.Build().Should().BeEmpty();
    }

    [Fact]
    public void Read_withoutAPackageDocument_throws()
    {
        using var package = PackageBuilder.Build(("mimetype", "application/epub+zip"));

        var read = () => EpubTextReader.Read(package, new TextCollector(), NullLogger.Instance);

        read.Should().Throw<InvalidDataException>();
    }

    private static string Read(Stream package)
    {
        var collector = new TextCollector();

        var status = EpubTextReader.Read(package, collector, NullLogger.Instance);

        status.Should().BeNull();
        return collector.Build();
    }
}