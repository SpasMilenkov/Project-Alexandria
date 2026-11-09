using Alexandria.Services.Preview.Documents.Metadata;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alexandria.Tests.Unit.Documents;

public class EpubReaderTests
{
    private const string Container = """
        <?xml version="1.0" encoding="utf-8"?>
        <container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0">
          <rootfiles>
            <rootfile full-path="epub/content.opf" media-type="application/oebps-package+xml"/>
          </rootfiles>
        </container>
        """;

    // Roles, title types and series details hang off refining meta elements in EPUB 3.
    private const string Epub3Package = """
        <?xml version="1.0" encoding="utf-8"?>
        <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="uid">
          <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
            <dc:identifier id="uid">urn:isbn:978-0-306-40615-7</dc:identifier>
            <dc:title id="title-short">Crusoe</dc:title>
            <meta property="title-type" refines="#title-short">short</meta>
            <dc:title id="title">The Life and Adventures of Robinson Crusoe</dc:title>
            <meta property="title-type" refines="#title">main</meta>
            <dc:creator id="author">Daniel Defoe</dc:creator>
            <meta property="role" refines="#author" scheme="marc:relators">aut</meta>
            <dc:creator id="illustrator">John Charles Dollman</dc:creator>
            <meta property="role" refines="#illustrator" scheme="marc:relators">ill</meta>
            <dc:contributor id="translator">Ana Doe</dc:contributor>
            <meta property="role" refines="#translator" scheme="marc:relators">trl</meta>
            <dc:publisher>Standard Ebooks</dc:publisher>
            <dc:language>en-GB</dc:language>
            <dc:subject>Islands--Fiction</dc:subject>
            <dc:subject>Adventure stories</dc:subject>
            <dc:date>2017-12-09T18:37:23Z</dc:date>
            <meta property="dcterms:modified">2026-10-30T17:30:39Z</meta>
            <meta property="schema:wordCount">121071</meta>
            <meta id="list" property="belongs-to-collection">Best 100 Novels</meta>
            <meta property="collection-type" refines="#list">set</meta>
            <meta property="group-position" refines="#list">2</meta>
            <meta id="series" property="belongs-to-collection">Crusoe Trilogy</meta>
            <meta property="collection-type" refines="#series">series</meta>
            <meta property="group-position" refines="#series">1</meta>
          </metadata>
          <manifest/>
          <spine/>
        </package>
        """;

    // EPUB 2 writes roles and events as opf: attributes, and calibre keeps series in name/content pairs.
    private const string Epub2Package = """
        <?xml version="1.0" encoding="utf-8"?>
        <package xmlns="http://www.idpf.org/2007/opf" version="2.0" unique-identifier="id">
          <metadata xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:opf="http://www.idpf.org/2007/opf">
            <dc:identifier id="id" opf:scheme="ISBN">0-8044-2957-X</dc:identifier>
            <dc:identifier opf:scheme="calibre">5f1c</dc:identifier>
            <dc:title>Foundation and Empire</dc:title>
            <dc:creator opf:role="aut" opf:file-as="Asimov, Isaac">Isaac Asimov</dc:creator>
            <dc:contributor opf:role="bkp">calibre (7.2.0) [https://calibre-ebook.com]</dc:contributor>
            <dc:date opf:event="publication">1952-01-01</dc:date>
            <dc:date opf:event="modification">2024-05-02</dc:date>
            <dc:language>en</dc:language>
            <meta name="calibre:series" content="Foundation"/>
            <meta name="calibre:series_index" content="2.0"/>
            <meta name="generator" content="Sigil/2.1.0"/>
          </metadata>
          <manifest/>
          <spine/>
        </package>
        """;

    private const string FontObfuscationOnly = """
        <encryption xmlns="urn:oasis:names:tc:opendocument:xmlns:container"
            xmlns:enc="http://www.w3.org/2001/04/xmlenc#">
          <enc:EncryptedData>
            <enc:EncryptionMethod Algorithm="http://www.idpf.org/2008/embedding"/>
            <enc:CipherData><enc:CipherReference URI="epub/fonts/league-spartan.otf"/></enc:CipherData>
          </enc:EncryptedData>
        </encryption>
        """;

    private const string EncryptedChapters = """
        <encryption xmlns="urn:oasis:names:tc:opendocument:xmlns:container"
            xmlns:enc="http://www.w3.org/2001/04/xmlenc#">
          <enc:EncryptedData>
            <enc:EncryptionMethod Algorithm="http://www.w3.org/2001/04/xmlenc#aes128-cbc"/>
            <enc:CipherData><enc:CipherReference URI="epub/text/chapter-1.xhtml"/></enc:CipherData>
          </enc:EncryptedData>
        </encryption>
        """;

    private static IReadOnlyDictionary<string, string> Read(params (string Path, string Content)[] parts)
    {
        using var package = PackageBuilder.Build(parts);

        return EpubReader.Read(package, NullLogger.Instance).Values;
    }

    [Fact]
    public void Read_epub3_resolvesRefinedTitlesRolesAndDates()
    {
        var fields = Read(("META-INF/container.xml", Container), ("epub/content.opf", Epub3Package));

        fields["package:version"].Should().Be("3.0");
        fields["opf:title"].Should().Be("The Life and Adventures of Robinson Crusoe");
        fields["opf:title:short"].Should().Be("Crusoe");
        fields["opf:creator"].Should().Be("Daniel Defoe");
        fields["opf:creator:ill"].Should().Be("John Charles Dollman");
        fields["opf:contributor:trl"].Should().Be("Ana Doe");
        fields["opf:publisher"].Should().Be("Standard Ebooks");
        fields["opf:language"].Should().Be("en-GB");
        fields["opf:subject"].Should().Be("Islands--Fiction; Adventure stories");
        fields["opf:date"].Should().Be("2017-12-09T18:37:23Z");
        fields["opf-meta:dcterms:modified"].Should().Be("2026-10-30T17:30:39Z");
        fields["opf-meta:schema:wordCount"].Should().Be("121071");
        fields["opf:isbn"].Should().Be("urn:isbn:978-0-306-40615-7");
    }

    [Fact]
    public void Read_epub3_takesOnlyExplicitSeriesAsSeries()
    {
        var fields = Read(("META-INF/container.xml", Container), ("epub/content.opf", Epub3Package));

        fields["opf:series"].Should().Be("Crusoe Trilogy");
        fields["opf:series-index"].Should().Be("1");
        fields["opf:collection:set"].Should().Be("Best 100 Novels");
    }

    [Fact]
    public void Read_epub2_readsRoleAndEventAttributesAndNamedMeta()
    {
        var fields = Read(("META-INF/container.xml", Container), ("epub/content.opf", Epub2Package));

        fields["opf:title"].Should().Be("Foundation and Empire");
        fields["opf:creator"].Should().Be("Isaac Asimov");
        fields["opf:contributor:bkp"].Should().StartWith("calibre");
        fields["opf:date:publication"].Should().Be("1952-01-01");
        fields["opf:date:modification"].Should().Be("2024-05-02");
        fields["opf:isbn"].Should().Be("0-8044-2957-X");
        fields["opf:identifier"].Should().Be("0-8044-2957-X; 5f1c");
        fields["opf-meta:calibre:series"].Should().Be("Foundation");
        fields["opf-meta:calibre:series_index"].Should().Be("2.0");
        fields["opf-meta:generator"].Should().Be("Sigil/2.1.0");
    }

    [Fact]
    public void Read_withoutContainer_fallsBackToThePackageDocument()
    {
        var fields = Read(("OEBPS/book.opf", Epub2Package));

        fields["opf:title"].Should().Be("Foundation and Empire");
    }

    [Fact]
    public void Read_fontObfuscation_isNotCopyProtection()
    {
        var fields = Read(
            ("META-INF/container.xml", Container),
            ("META-INF/encryption.xml", FontObfuscationOnly),
            ("epub/content.opf", Epub3Package));

        fields.Should().NotContainKey("package:drm");
    }

    [Fact]
    public void Read_encryptedChapters_areCopyProtectedButMetadataIsStillRead()
    {
        var fields = Read(
            ("META-INF/container.xml", Container),
            ("META-INF/encryption.xml", EncryptedChapters),
            ("epub/content.opf", Epub3Package));

        fields["package:drm"].Should().Be("unknown");
        fields["opf:title"].Should().Be("The Life and Adventures of Robinson Crusoe");
    }

    [Theory]
    [InlineData("META-INF/rights.xml", "adobe-adept")]
    [InlineData("META-INF/license.lcpl", "readium-lcp")]
    [InlineData("META-INF/sinf.xml", "apple-fairplay")]
    public void Read_licenseFile_namesTheScheme(string path, string scheme)
    {
        var fields = Read(
            ("META-INF/container.xml", Container),
            (path, "<license/>"),
            ("epub/content.opf", Epub3Package));

        fields["package:drm"].Should().Be(scheme);
    }

    [Fact]
    public void Read_malformedPackage_returnsNothingInsteadOfThrowing()
    {
        var fields = Read(("META-INF/container.xml", Container), ("epub/content.opf", "<package><metadata>"));

        fields.Should().BeEmpty();
    }

    [Fact]
    public void Read_packageWithDoctype_isRejected()
    {
        const string withDtd = """
            <?xml version="1.0"?>
            <!DOCTYPE package [<!ENTITY x "boom">]>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:title>&x;</dc:title></metadata>
            </package>
            """;

        var fields = Read(("META-INF/container.xml", Container), ("epub/content.opf", withDtd));

        fields.Should().NotContainKey("opf:title");
    }
}
