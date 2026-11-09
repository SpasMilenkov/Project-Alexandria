using Alexandria.Common.Services;
using Alexandria.Services.Preview.Documents.Metadata;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Documents;

public class DocumentMetadataMapperTests
{
    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string Pptx = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Odt = "application/vnd.oasis.opendocument.text";
    private const string Ods = "application/vnd.oasis.opendocument.spreadsheet";
    private const string Pdf = "application/pdf";
    private const string Epub = "application/epub+zip";
    private const string Azw3 = "application/vnd.amazon.mobi8-ebook";

    private static readonly Guid VersionId = Guid.NewGuid();

    private static DocumentFields Fields(params (string Key, string Value)[] values)
        => new(values.ToDictionary(v => v.Key, v => v.Value), IsPasswordProtected: false);

    [Fact]
    public void ToEntity_wordDocument_mapsCoreAndExtendedProperties()
    {
        var fields = Fields(
            ("core:title", "Quarterly report"),
            ("core:creator", "Ana Doe"),
            ("core:lastModifiedBy", "Ben Roe"),
            ("core:created", "2024-03-01T10:20:00Z"),
            ("app:Company", "Acme"),
            ("app:Application", "Microsoft Office Word"),
            ("app:Pages", "3"),
            ("app:Words", "1200"),
            ("app:TotalTime", "42"));

        var entity = DocumentMetadataMapper.ToEntity(VersionId, Docx, fields);

        entity.FileVersionId.Should().Be(VersionId);
        entity.Title.Should().Be("Quarterly report");
        entity.Author.Should().Be("Ana Doe");
        entity.LastModifiedBy.Should().Be("Ben Roe");
        entity.Company.Should().Be("Acme");
        entity.Application.Should().Be("Microsoft Office Word");
        entity.AuthoredAt.Should().Be(new DateTime(2024, 3, 1, 10, 20, 0, DateTimeKind.Utc));
        entity.AuthoredOffsetMinutes.Should().Be(0);
        entity.PageCount.Should().Be(3);
        entity.WordCount.Should().Be(1200);
        entity.EditingMinutes.Should().Be(42);
        entity.ExtractorVersion.Should().Be(DocumentMetadataExtractor.CurrentVersion);
        entity.RawJson.Should().Contain("\"core:title\":\"Quarterly report\"");
    }

    [Fact]
    public void ToEntity_presentation_prefersTheRecordedSlideCountOverCountedParts()
    {
        var recorded = DocumentMetadataMapper.ToEntity(VersionId, Pptx, Fields(("app:Slides", "12"), ("package:slides", "11")));
        var counted = DocumentMetadataMapper.ToEntity(VersionId, Pptx, Fields(("package:slides", "11")));

        recorded.SlideCount.Should().Be(12);
        counted.SlideCount.Should().Be(11);
        counted.PageCount.Should().BeNull();
    }

    [Fact]
    public void ToEntity_countsOnlyApplyToTheirOwnKindOfDocument()
    {
        var workbook = DocumentMetadataMapper.ToEntity(VersionId, Xlsx, Fields(("app:Pages", "4"), ("package:sheets", "2")));
        var textWithTables = DocumentMetadataMapper.ToEntity(VersionId, Odt, Fields(("odf-stat:table-count", "5")));
        var spreadsheet = DocumentMetadataMapper.ToEntity(VersionId, Ods, Fields(("odf-stat:table-count", "5")));

        workbook.PageCount.Should().BeNull();
        workbook.SheetCount.Should().Be(2);
        textWithTables.SheetCount.Should().BeNull();
        spreadsheet.SheetCount.Should().Be(5);
    }

    [Fact]
    public void ToEntity_openDocument_mapsInitialCreatorAsAuthorAndCleansTheGenerator()
    {
        var fields = Fields(
            ("odf:initial-creator", "Ana Doe"),
            ("odf:creator", "Ben Roe"),
            ("odf:generator", "LibreOffice/24.2.7.2$Linux_X86_64 LibreOffice_project/420$Build-2"),
            ("odf:creation-date", "2026-01-08T19:25:31.437770626"),
            ("odf:editing-duration", "PT1H7M40S"),
            ("odf-stat:page-count", "3"));

        var entity = DocumentMetadataMapper.ToEntity(VersionId, Odt, fields);

        entity.Author.Should().Be("Ana Doe");
        entity.LastModifiedBy.Should().Be("Ben Roe");
        entity.Application.Should().Be("LibreOffice 24.2.7.2");
        entity.AuthoredAt.Should().Be(new DateTime(2026, 1, 8, 19, 25, 31, DateTimeKind.Utc).AddTicks(4377706));
        entity.AuthoredOffsetMinutes.Should().BeNull();
        entity.EditingMinutes.Should().Be(68);
        entity.PageCount.Should().Be(3);
    }

    [Fact]
    public void ToEntity_pdf_prefersTheInfoDictionaryAndFallsBackToXmp()
    {
        var fields = Fields(
            ("pdf:Title", "From info"),
            ("xmp:dc:title", "From XMP"),
            ("xmp:dc:creator", "Ana Doe"),
            ("xmp:dc:language", "en-GB"),
            ("pdf:Creator", "Writer"),
            ("pdf:Producer", "LibreOffice 24.2"),
            ("pdf:CreationDate", "not a date"),
            ("xmp:xmp:CreateDate", "2026-01-08T20:26:20+02"),
            ("pdf:Pages", "3"),
            ("pdf:Page size", "612 x 792 pts (letter)"));

        var entity = DocumentMetadataMapper.ToEntity(VersionId, Pdf, fields);

        entity.Title.Should().Be("From info");
        entity.Author.Should().Be("Ana Doe");
        entity.Language.Should().Be("en-GB");
        entity.Application.Should().Be("Writer");
        entity.Producer.Should().Be("LibreOffice 24.2");
        entity.AuthoredAt.Should().Be(new DateTime(2026, 1, 8, 18, 26, 20, DateTimeKind.Utc));
        entity.AuthoredOffsetMinutes.Should().Be(120);
        entity.PageCount.Should().Be(3);
        entity.PaperSize.Should().Be("Letter");
    }

    [Fact]
    public void ToEntity_passwordProtected_keepsTheFlag()
    {
        var entity = DocumentMetadataMapper.ToEntity(VersionId, Pdf, DocumentFields.PasswordProtected);

        entity.IsPasswordProtected.Should().BeTrue();
        entity.Title.Should().BeNull();
        entity.RawJson.Should().Be("{}");
    }

    [Fact]
    public void ToEntity_unsupportedType_throws()
    {
        var act = () => DocumentMetadataMapper.ToEntity(VersionId, "application/msword", Fields());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToEntity_textWithLineBreaksAndControlCharacters_becomesOneLine()
    {
        var entity = DocumentMetadataMapper.ToEntity(VersionId, Pdf, Fields(("pdf:Title", "  First\n\tsecond\u0007  third ")));

        entity.Title.Should().Be("First second third");
    }

    [Fact]
    public void ToEntity_overlongText_isTruncatedToItsColumn()
    {
        var entity = DocumentMetadataMapper.ToEntity(VersionId, Pdf, Fields(("pdf:Author", new string('a', 400))));

        entity.Author.Should().HaveLength(255);
    }

    [Theory]
    [InlineData("2024-03-01T10:20:00Z", 2024, 3, 1, 10, 20, 0)]
    [InlineData("2026-01-08T20:26:20+02", 2026, 1, 8, 18, 26, 20)]
    [InlineData("2026-01-08T20:26:20+04:30", 2026, 1, 8, 15, 56, 20)]
    [InlineData("2026-01-08T20:26:20-0700", 2026, 1, 9, 3, 26, 20)]
    [InlineData("2026-01-08T20:26", 2026, 1, 8, 20, 26, 0)]
    [InlineData("2026-01-08", 2026, 1, 8, 0, 0, 0)]
    public void ParseDate_shapesTheseFormatsWrite_areRead(
        string value, int year, int month, int day, int hour, int minute, int second)
    {
        var parsed = DocumentMetadataMapper.ParseDate(value);

        parsed.Should().NotBeNull();
        parsed!.Value.Value.Should().Be(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("1601-01-01T00:00:00Z")]
    [InlineData("2999-01-01T00:00:00Z")]
    [InlineData("0000-00-00T00:00:00")]
    [InlineData("2026-13-01T00:00:00Z")]
    [InlineData("2026-01-08T20:26:20+19")]
    [InlineData("yesterday")]
    [InlineData("")]
    public void ParseDate_implausibleOrMalformed_isNull(string value)
    {
        DocumentMetadataMapper.ParseDate(value).Should().BeNull();
    }

    [Theory]
    [InlineData("PT7M11S", 7)]
    [InlineData("PT18M22S", 18)]
    [InlineData("P1DT2H", 1560)]
    [InlineData("PT20S", null)]
    [InlineData("PT0S", null)]
    [InlineData("7 minutes", null)]
    public void DurationMinutes_isoDurations_roundToMinutes(string value, int? expected)
    {
        DocumentMetadataMapper.DurationMinutes(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("595.32 x 841.92 pts (A4)", "A4")]
    [InlineData("612 x 792 pts (letter)", "Letter")]
    [InlineData("960 x 540 pts", null)]
    public void PaperSize_namedSizes_areRead(string value, string? expected)
    {
        DocumentMetadataMapper.PaperSize(value).Should().Be(expected);
    }

    [Fact]
    public void ToEntity_epub_mapsBookFields()
    {
        var fields = Fields(
            ("opf:title", "The Life and Adventures of Robinson Crusoe"),
            ("opf:creator", "Daniel Defoe"),
            ("opf:publisher", "Standard Ebooks"),
            ("opf:language", "en-GB"),
            ("opf:subject", "Islands--Fiction; Adventure stories"),
            ("opf-meta:schema:abstract", "Shipwrecked on a remote tropical island."),
            ("opf:date", "2017-12-09T18:37:23Z"),
            ("opf-meta:dcterms:modified", "2026-10-30T17:30:39Z"),
            ("opf-meta:schema:wordCount", "121071"),
            ("opf:isbn", "urn:isbn:978-0-306-40615-7"),
            ("opf:series", "Crusoe Trilogy"),
            ("opf:series-index", "1"));

        var entity = DocumentMetadataMapper.ToEntity(VersionId, Epub, fields);

        entity.Title.Should().Be("The Life and Adventures of Robinson Crusoe");
        entity.Author.Should().Be("Daniel Defoe");
        entity.Publisher.Should().Be("Standard Ebooks");
        entity.Language.Should().Be("en-GB");
        entity.Subject.Should().Be("Shipwrecked on a remote tropical island.");
        entity.Keywords.Should().Be("Islands--Fiction; Adventure stories");
        entity.AuthoredAt.Should().Be(new DateTime(2017, 12, 9, 18, 37, 23, DateTimeKind.Utc));
        entity.ModifiedAt.Should().Be(new DateTime(2026, 10, 30, 17, 30, 39, DateTimeKind.Utc));
        entity.WordCount.Should().Be(121071);
        entity.Isbn.Should().Be("9780306406157");
        entity.Series.Should().Be("Crusoe Trilogy");
        entity.SeriesIndex.Should().Be(1m);
        entity.IsDrmProtected.Should().BeFalse();
        entity.IsPasswordProtected.Should().BeFalse();
    }

    [Fact]
    public void ToEntity_epub2_fallsBackToCalibreSeriesAndEventDates()
    {
        var fields = Fields(
            ("opf:date:publication", "2001-03-04"),
            ("opf:date:modification", "2024-05-02"),
            ("opf-meta:calibre:series", "Foundation"),
            ("opf-meta:calibre:series_index", "2.5"),
            ("opf-meta:generator", "Sigil/2.1.0"),
            ("opf:language", "en; fr"));

        var entity = DocumentMetadataMapper.ToEntity(VersionId, Epub, fields);

        entity.AuthoredAt.Should().Be(new DateTime(2001, 3, 4, 0, 0, 0, DateTimeKind.Utc));
        entity.ModifiedAt.Should().Be(new DateTime(2024, 5, 2, 0, 0, 0, DateTimeKind.Utc));
        entity.Series.Should().Be("Foundation");
        entity.SeriesIndex.Should().Be(2.5m);
        entity.Application.Should().Be("Sigil 2.1.0");
        entity.Language.Should().Be("en");
    }

    [Fact]
    public void ToEntity_epubWithDrm_isFlaggedButKeepsItsFields()
    {
        var fields = Fields(("package:drm", "adobe-adept"), ("opf:title", "Locked novel"));

        var entity = DocumentMetadataMapper.ToEntity(VersionId, Epub, fields);

        entity.IsDrmProtected.Should().BeTrue();
        entity.IsPasswordProtected.Should().BeFalse();
        entity.Title.Should().Be("Locked novel");
    }

    [Fact]
    public void ToEntity_kindleBook_mapsExthRecords()
    {
        var fields = Fields(
            ("mobi:title", "Robinson Crusoe"),
            ("exth:title", "The Life and Adventures of Robinson Crusoe"),
            ("exth:author", "Daniel Defoe"),
            ("exth:publisher", "Standard Ebooks"),
            ("exth:subject", "Islands--Fiction; Adventure stories"),
            ("exth:published", "2017-12-09T18:37:23+00:00"),
            ("exth:language", "en"),
            ("exth:isbn", "0-306-40615-2"));

        var entity = DocumentMetadataMapper.ToEntity(VersionId, Azw3, fields);

        entity.Title.Should().Be("The Life and Adventures of Robinson Crusoe");
        entity.Author.Should().Be("Daniel Defoe");
        entity.Publisher.Should().Be("Standard Ebooks");
        entity.Keywords.Should().Be("Islands--Fiction; Adventure stories");
        entity.Language.Should().Be("en");
        entity.AuthoredAt.Should().Be(new DateTime(2017, 12, 9, 18, 37, 23, DateTimeKind.Utc));
        entity.AuthoredOffsetMinutes.Should().Be(0);
        entity.ModifiedAt.Should().BeNull();
        entity.Isbn.Should().Be("9780306406157");
        entity.Subject.Should().BeNull();
        entity.Application.Should().BeNull();
        entity.IsDrmProtected.Should().BeFalse();
    }

    [Fact]
    public void ToEntity_kindleBookWithoutUpdatedTitle_usesTheHeaderTitleAndFlagsDrm()
    {
        var fields = Fields(("mobi:title", "Locked novel"), ("mobi:drm", "mobipocket"), ("exth:isbn", "not-an-isbn"));

        var entity = DocumentMetadataMapper.ToEntity(VersionId, "application/x-mobipocket-ebook", fields);

        entity.Title.Should().Be("Locked novel");
        entity.IsDrmProtected.Should().BeTrue();
        entity.Isbn.Should().BeNull();
    }

    [Theory]
    [InlineData("opf:identifier", "9780306406157", "9780306406157")]
    [InlineData("opf:identifier", "0306406152", null)]
    [InlineData("opf:identifier", "https://standardebooks.org/ebooks/daniel-defoe", null)]
    [InlineData("opf:isbn", "0306406152", "9780306406157")]
    public void ToEntity_epubIsbn_unmarkedIdentifiersCountOnlyInThirteenDigitForm(
        string key, string value, string? expected)
    {
        var entity = DocumentMetadataMapper.ToEntity(VersionId, Epub, Fields((key, value)));

        entity.Isbn.Should().Be(expected);
    }

    [Theory]
    [InlineData("978-0-306-40615-7", "9780306406157")]
    [InlineData("urn:isbn:9780306406157", "9780306406157")]
    [InlineData("ISBN 0-306-40615-2", "9780306406157")]
    [InlineData("0-8044-2957-X", "9780804429573")]
    [InlineData("979-1-234-56789-6", "9791234567896")]
    [InlineData("0-306-40615-3", null)]
    [InlineData("978-0-306-40615-8", null)]
    [InlineData("123-0-306-40615-7", null)]
    [InlineData("X306406152", null)]
    [InlineData("", null)]
    public void Isbn13_normalizesValidFormsAndRejectsBadCheckDigits(string value, string? expected)
    {
        DocumentMetadataMapper.Isbn13(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("2", 2.0)]
    [InlineData("2.0", 2.0)]
    [InlineData("2.555", 2.56)]
    [InlineData("0", null)]
    [InlineData("-1", null)]
    [InlineData("two", null)]
    [InlineData(null, null)]
    public void SeriesIndex_keepsPositivePositions(string? value, double? expected)
    {
        DocumentMetadataMapper.SeriesIndex(value).Should().Be(expected is null ? null : (decimal)expected.Value);
    }
}
