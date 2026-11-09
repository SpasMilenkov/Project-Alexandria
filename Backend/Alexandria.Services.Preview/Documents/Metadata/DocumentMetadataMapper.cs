using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alexandria.Common.Helpers;
using Alexandria.Common.Mime;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Services.Preview.Documents.Metadata;

/// <summary>
/// Turns raw reader fields into typed columns. Anything malformed becomes null rather than a
/// guess, because a wrong authoring date is worse than a missing one.
/// </summary>
internal static partial class DocumentMetadataMapper
{
    private const int FirstPlausibleYear = 1970;
    private const int MaxOffsetMinutes = 18 * 60;
    private const int MaxFractionDigits = 7;
    private const int LongText = ValidationConstants.StringLengths.LongString;
    private const int MediumText = ValidationConstants.StringLengths.MediumString;
    private const int ShortText = ValidationConstants.StringLengths.ShortString;

    // A clock set ahead can stamp a file a little into the future, but not by more than this.
    private static readonly TimeSpan FutureTolerance = TimeSpan.FromDays(1);

    private static readonly JsonSerializerOptions RawJsonOptions = new() { WriteIndented = false };

    public static DocumentMetadata ToEntity(Guid versionId, string mimeType, DocumentFields fields)
    {
        var format = DocumentMetadataFileTypes.FormatOf(mimeType)
                     ?? throw new ArgumentException($"No document metadata reader for '{mimeType}'.", nameof(mimeType));
        var category = MimeCategories.Categorize(mimeType);

        return format switch
        {
            DocumentMetadataFormat.OfficeOpenXml => FromOfficeOpenXml(versionId, category, fields),
            DocumentMetadataFormat.OpenDocument => FromOpenDocument(versionId, category, fields),
            DocumentMetadataFormat.Pdf => FromPdf(versionId, fields),
            DocumentMetadataFormat.Epub => FromEpub(versionId, fields),
            DocumentMetadataFormat.Mobi => FromMobi(versionId, fields),
            _ => throw new ArgumentOutOfRangeException(nameof(mimeType), format, "No mapping for this format.")
        };
    }

    private static DocumentMetadata FromOfficeOpenXml(Guid versionId, FileCategory category, DocumentFields fields)
    {
        var f = fields.Values;
        var authored = ParseDate(f.GetValueOrDefault("core:created"));
        var modified = ParseDate(f.GetValueOrDefault("core:modified"));

        return new DocumentMetadata
        {
            Id = Guid.NewGuid(),
            FileVersionId = versionId,
            Title = Text(f, "core:title", LongText),
            Subject = Text(f, "core:subject", LongText),
            Keywords = Text(f, "core:keywords", LongText),
            Language = Text(f, "core:language", ShortText),
            Author = Text(f, "core:creator", MediumText),
            LastModifiedBy = Text(f, "core:lastModifiedBy", MediumText),
            Company = Text(f, "app:Company", MediumText),
            Application = ApplicationName(f.GetValueOrDefault("app:Application")),
            AuthoredAt = authored?.Value,
            AuthoredOffsetMinutes = authored?.OffsetMinutes,
            ModifiedAt = modified?.Value,
            ModifiedOffsetMinutes = modified?.OffsetMinutes,
            PageCount = category == FileCategory.Document ? PositiveInt(f, "app:Pages") : null,
            WordCount = category == FileCategory.Document ? PositiveInt(f, "app:Words") : null,
            SlideCount = category == FileCategory.Presentation
                ? PositiveInt(f, "app:Slides") ?? PositiveInt(f, "package:slides")
                : null,
            SheetCount = category == FileCategory.Spreadsheet ? PositiveInt(f, "package:sheets") : null,
            EditingMinutes = PositiveInt(f, "app:TotalTime"),
            IsPasswordProtected = fields.IsPasswordProtected,
            RawJson = JsonSerializer.Serialize(f, RawJsonOptions),
            ExtractorVersion = DocumentMetadataExtractor.CurrentVersion
        };
    }

    private static DocumentMetadata FromOpenDocument(Guid versionId, FileCategory category, DocumentFields fields)
    {
        var f = fields.Values;
        var authored = ParseDate(f.GetValueOrDefault("odf:creation-date"));
        var modified = ParseDate(f.GetValueOrDefault("odf:date"));

        // The table count means sheets in a spreadsheet but tables on the page in a text document.
        return new DocumentMetadata
        {
            Id = Guid.NewGuid(),
            FileVersionId = versionId,
            Title = Text(f, "odf:title", LongText),
            Subject = Text(f, "odf:subject", LongText),
            Keywords = Text(f, "odf:keyword", LongText),
            Language = Text(f, "odf:language", ShortText),
            Author = Text(f, "odf:initial-creator", MediumText),
            LastModifiedBy = Text(f, "odf:creator", MediumText),
            Application = ApplicationName(f.GetValueOrDefault("odf:generator")),
            AuthoredAt = authored?.Value,
            AuthoredOffsetMinutes = authored?.OffsetMinutes,
            ModifiedAt = modified?.Value,
            ModifiedOffsetMinutes = modified?.OffsetMinutes,
            PageCount = category == FileCategory.Document ? PositiveInt(f, "odf-stat:page-count") : null,
            WordCount = category == FileCategory.Document ? PositiveInt(f, "odf-stat:word-count") : null,
            SlideCount = category == FileCategory.Presentation ? PositiveInt(f, "package:pages") : null,
            SheetCount = category == FileCategory.Spreadsheet ? PositiveInt(f, "odf-stat:table-count") : null,
            EditingMinutes = DurationMinutes(f.GetValueOrDefault("odf:editing-duration")),
            IsPasswordProtected = fields.IsPasswordProtected,
            RawJson = JsonSerializer.Serialize(f, RawJsonOptions),
            ExtractorVersion = DocumentMetadataExtractor.CurrentVersion
        };
    }

    // The info dictionary wins over XMP when both are set, because pdfinfo has already decoded
    // its text and dates. XMP fills the gaps, which in PDF 2.0 files can be everything.
    private static DocumentMetadata FromPdf(Guid versionId, DocumentFields fields)
    {
        var f = fields.Values;
        var authored = ParseDate(f.GetValueOrDefault("pdf:CreationDate")) ?? ParseDate(f.GetValueOrDefault("xmp:xmp:CreateDate"));
        var modified = ParseDate(f.GetValueOrDefault("pdf:ModDate")) ?? ParseDate(f.GetValueOrDefault("xmp:xmp:ModifyDate"));

        return new DocumentMetadata
        {
            Id = Guid.NewGuid(),
            FileVersionId = versionId,
            Title = Text(f, "pdf:Title", LongText) ?? Text(f, "xmp:dc:title", LongText),
            Subject = Text(f, "pdf:Subject", LongText) ?? Text(f, "xmp:dc:description", LongText),
            Keywords = Text(f, "pdf:Keywords", LongText)
                       ?? Text(f, "xmp:pdf:Keywords", LongText)
                       ?? Text(f, "xmp:dc:subject", LongText),
            Language = Text(f, "xmp:dc:language", ShortText),
            Author = Text(f, "pdf:Author", MediumText) ?? Text(f, "xmp:dc:creator", MediumText),
            Application = ApplicationName(f.GetValueOrDefault("pdf:Creator"))
                          ?? ApplicationName(f.GetValueOrDefault("xmp:xmp:CreatorTool")),
            Producer = ApplicationName(f.GetValueOrDefault("pdf:Producer"))
                       ?? ApplicationName(f.GetValueOrDefault("xmp:pdf:Producer")),
            AuthoredAt = authored?.Value,
            AuthoredOffsetMinutes = authored?.OffsetMinutes,
            ModifiedAt = modified?.Value,
            ModifiedOffsetMinutes = modified?.OffsetMinutes,
            PageCount = PositiveInt(f, "pdf:Pages"),
            PaperSize = PaperSize(f.GetValueOrDefault("pdf:Page size")),
            IsPasswordProtected = fields.IsPasswordProtected,
            RawJson = JsonSerializer.Serialize(f, RawJsonOptions),
            ExtractorVersion = DocumentMetadataExtractor.CurrentVersion
        };
    }

    // A book's date is when this edition came out, and the short abstract is the closest thing
    // to a subject line. The long description is HTML, so it stays in the raw fields.
    private static DocumentMetadata FromEpub(Guid versionId, DocumentFields fields)
    {
        var f = fields.Values;
        var published = ParseDate(f.GetValueOrDefault("opf:date:publication"))
                        ?? ParseDate(f.GetValueOrDefault("opf:date"));
        var modified = ParseDate(f.GetValueOrDefault("opf-meta:dcterms:modified"))
                       ?? ParseDate(f.GetValueOrDefault("opf:date:modification"));

        return new DocumentMetadata
        {
            Id = Guid.NewGuid(),
            FileVersionId = versionId,
            Title = Text(f, "opf:title", LongText),
            Subject = Text(f, "opf-meta:schema:abstract", LongText),
            Keywords = Text(f, "opf:subject", LongText),
            Language = FirstListItem(Text(f, "opf:language", ShortText)),
            Author = Text(f, "opf:creator", MediumText),
            Application = ApplicationName(f.GetValueOrDefault("opf-meta:generator")),
            AuthoredAt = published?.Value,
            AuthoredOffsetMinutes = published?.OffsetMinutes,
            ModifiedAt = modified?.Value,
            ModifiedOffsetMinutes = modified?.OffsetMinutes,
            WordCount = PositiveInt(f, "opf-meta:schema:wordCount"),
            Publisher = Text(f, "opf:publisher", MediumText),
            Isbn = BookIsbn(f),
            Series = Text(f, "opf:series", MediumText) ?? Text(f, "opf-meta:calibre:series", MediumText),
            SeriesIndex = SeriesIndex(f.GetValueOrDefault("opf:series-index"))
                          ?? SeriesIndex(f.GetValueOrDefault("opf-meta:calibre:series_index")),
            IsDrmProtected = f.ContainsKey("package:drm"),
            RawJson = JsonSerializer.Serialize(f, RawJsonOptions),
            ExtractorVersion = DocumentMetadataExtractor.CurrentVersion
        };
    }

    // Kindle books record no modification date, word count or series. The description is HTML,
    // so as with EPUB it stays in the raw fields, and the updated title wins over the header's.
    private static DocumentMetadata FromMobi(Guid versionId, DocumentFields fields)
    {
        var f = fields.Values;
        var published = ParseDate(f.GetValueOrDefault("exth:published"));

        return new DocumentMetadata
        {
            Id = Guid.NewGuid(),
            FileVersionId = versionId,
            Title = Text(f, "exth:title", LongText) ?? Text(f, "mobi:title", LongText),
            Keywords = Text(f, "exth:subject", LongText),
            Language = FirstListItem(Text(f, "exth:language", ShortText)),
            Author = Text(f, "exth:author", MediumText),
            AuthoredAt = published?.Value,
            AuthoredOffsetMinutes = published?.OffsetMinutes,
            Publisher = Text(f, "exth:publisher", MediumText),
            Isbn = ListItems(f.GetValueOrDefault("exth:isbn")).Select(Isbn13).FirstOrDefault(isbn => isbn is not null),
            IsDrmProtected = f.ContainsKey("mobi:drm"),
            RawJson = JsonSerializer.Serialize(f, RawJsonOptions),
            ExtractorVersion = DocumentMetadataExtractor.CurrentVersion
        };
    }

    /// <summary>
    /// An identifier marked as an ISBN wins. Failing that, an unmarked identifier counts only in
    /// the 13-digit Bookland form, since a stray 10-digit number passes the check one time in eleven.
    /// </summary>
    private static string? BookIsbn(IReadOnlyDictionary<string, string> fields)
    {
        foreach (var candidate in ListItems(fields.GetValueOrDefault("opf:isbn")))
        {
            var isbn = Isbn13(candidate);
            if (isbn is not null)
                return isbn;
        }

        foreach (var candidate in ListItems(fields.GetValueOrDefault("opf:identifier")))
        {
            var isbn = Isbn13(candidate);
            if (isbn is not null && Digits(candidate).Length == 13)
                return isbn;
        }

        return null;
    }

    /// <summary>
    /// The 13-digit form of an ISBN written as <c>urn:isbn:</c>, with hyphens or spaces, or in the
    /// older 10-digit form. Null when the check digit is wrong.
    /// </summary>
    internal static string? Isbn13(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var digits = Digits(value);

        if (digits.Length == 13 && (digits.StartsWith("978", StringComparison.Ordinal) || digits.StartsWith("979", StringComparison.Ordinal)))
            return Isbn13CheckDigit(digits[..12]) == digits[12] ? digits : null;

        if (digits.Length != 10 || !IsValidIsbn10(digits))
            return null;

        var body = "978" + digits[..9];

        return body + Isbn13CheckDigit(body);
    }

    // Keeps the digits and a trailing X, the ISBN-10 check digit for ten.
    private static string Digits(string value)
    {
        var trimmed = value.Trim();
        var colon = trimmed.LastIndexOf(':');
        if (colon >= 0)
            trimmed = trimmed[(colon + 1)..];

        if (trimmed.StartsWith("ISBN", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[4..];

        var builder = new StringBuilder(trimmed.Length);
        foreach (var c in trimmed)
        {
            if (char.IsAsciiDigit(c))
                builder.Append(c);
            else if (c is 'X' or 'x' && builder.Length == 9)
                builder.Append('X');
            else if (c is not ('-' or ' '))
                return "";
        }

        return builder.ToString();
    }

    private static bool IsValidIsbn10(string digits)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var digit = digits[i] == 'X' ? 10 : digits[i] - '0';
            if (digit == 10 && i != 9)
                return false;

            sum += digit * (10 - i);
        }

        return sum % 11 == 0;
    }

    private static char Isbn13CheckDigit(string twelveDigits)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            var digit = twelveDigits[i] - '0';
            sum += i % 2 == 0 ? digit : digit * 3;
        }

        return (char)('0' + (10 - sum % 10) % 10);
    }

    /// <summary>
    /// A position like <c>2</c> or calibre's <c>2.0</c>, kept to two decimals. Zero and negative
    /// positions are how tools spell "not in a series".
    /// </summary>
    internal static decimal? SeriesIndex(string? value)
    {
        var parsed = decimal.TryParse(
            value?.Trim(),
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var index);

        if (!parsed || index <= 0 || index >= 1_000_000)
            return null;

        return Math.Round(index, 2);
    }

    private static IEnumerable<string> ListItems(string? value)
        => string.IsNullOrEmpty(value)
            ? []
            : value.Split(DocumentFieldBag.ListSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? FirstListItem(string? value) => ListItems(value).FirstOrDefault();

    /// <summary>
    /// Reads the ISO 8601 shapes these formats write: OOXML's UTC W3CDTF, ODF's offset-free wall
    /// clock with up to nine fraction digits, and pdfinfo's offsets in hours (<c>+03</c>) or
    /// hours and minutes. Without an offset the wall clock is kept as is.
    /// </summary>
    internal static ParsedDate? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = IsoDate().Match(value.Trim());
        if (!match.Success)
            return null;

        var time = match.Groups["time"].Success ? match.Groups["time"].Value : "00:00:00";
        if (time.Length == 5)
            time += ":00";

        var parsed = DateTime.TryParseExact(
            $"{match.Groups["date"].Value}T{time}",
            "yyyy-MM-dd'T'HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var wallClock);

        if (!parsed)
            return null;

        var withFraction = wallClock.Add(ParseFraction(match.Groups["fraction"].Value));

        short? offsetMinutes = null;
        if (match.Groups["zone"].Success)
        {
            offsetMinutes = ParseOffsetMinutes(match.Groups["zone"].Value);
            if (offsetMinutes is null)
                return null;
        }

        var utc = DateTime.SpecifyKind(withFraction.AddMinutes(-(offsetMinutes ?? 0)), DateTimeKind.Utc);
        if (utc.Year < FirstPlausibleYear || utc > DateTime.UtcNow + FutureTolerance)
            return null;

        return new ParsedDate(utc, offsetMinutes);
    }

    private static TimeSpan ParseFraction(string digits)
    {
        if (digits.Length == 0)
            return TimeSpan.Zero;

        var kept = digits.Length > MaxFractionDigits ? digits[..MaxFractionDigits] : digits;
        var ticks = long.Parse(kept.PadRight(MaxFractionDigits, '0'), CultureInfo.InvariantCulture);

        return TimeSpan.FromTicks(ticks);
    }

    private static short? ParseOffsetMinutes(string zone)
    {
        if (zone == "Z")
            return 0;

        var digits = zone[1..].Replace(":", "", StringComparison.Ordinal);
        var hours = int.Parse(digits[..2], CultureInfo.InvariantCulture);
        var minutes = digits.Length == 4 ? int.Parse(digits[2..], CultureInfo.InvariantCulture) : 0;

        if (minutes >= 60)
            return null;

        var total = hours * 60 + minutes;
        if (total > MaxOffsetMinutes)
            return null;

        return (short)(zone[0] == '-' ? -total : total);
    }

    /// <summary>
    /// ODF stores editing time as an ISO 8601 duration such as <c>PT1H7M11S</c>.
    /// </summary>
    internal static int? DurationMinutes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = IsoDuration().Match(value.Trim());
        if (!match.Success)
            return null;

        var days = PartOrZero(match, "days");
        var hours = PartOrZero(match, "hours");
        var minutes = PartOrZero(match, "minutes");
        var seconds = PartOrZero(match, "seconds");

        var total = days * 24 * 60 + hours * 60 + minutes + seconds / 60;
        if (total < 1 || total > int.MaxValue)
            return null;

        return (int)Math.Round(total);
    }

    private static double PartOrZero(Match match, string group)
    {
        if (!match.Groups[group].Success)
            return 0;

        return double.Parse(match.Groups[group].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writers add build details nobody reads (<c>LibreOffice/24.2.7.2$Linux_X86_64
    /// LibreOffice_project/420$Build-2</c>), so the name stops at the first <c>$</c> and a
    /// <c>Name/1.2</c> pair reads as <c>Name 1.2</c>.
    /// </summary>
    internal static string? ApplicationName(string? value)
    {
        var text = Clean(value);
        if (text is null)
            return null;

        var dollar = text.IndexOf('$');
        if (dollar >= 0)
            text = text[..dollar].Trim();

        text = NameSlashVersion().Replace(text, "$1 $2", 1);

        return Truncate(text, MediumText);
    }

    /// <summary>
    /// pdfinfo names a standard size in brackets, as in <c>595.32 x 841.92 pts (A4)</c>.
    /// </summary>
    internal static string? PaperSize(string? value)
    {
        if (value is null)
            return null;

        var match = PaperSizeName().Match(value);
        if (!match.Success)
            return null;

        var name = match.Groups["name"].Value.Trim();
        if (name.Length == 0)
            return null;

        return Truncate(char.ToUpperInvariant(name[0]) + name[1..], ShortText);
    }

    private static string? Text(IReadOnlyDictionary<string, string> fields, string key, int maxLength)
    {
        var text = Clean(fields.GetValueOrDefault(key));

        return text is null ? null : Truncate(text, maxLength);
    }

    // Line breaks and control characters from the file would break a single-line label.
    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var builder = new StringBuilder(value.Length);
        var lastWasSpace = false;

        foreach (var c in value)
        {
            var isSpace = char.IsWhiteSpace(c) || char.IsControl(c);

            if (isSpace && !lastWasSpace)
                builder.Append(' ');
            else if (!isSpace)
                builder.Append(c);

            lastWasSpace = isSpace;
        }

        var cleaned = builder.ToString().Trim();

        return cleaned.Length == 0 ? null : cleaned;
    }

    private static string Truncate(string value, int maxLength) => value.Length > maxLength ? value[..maxLength] : value;

    private static int? PositiveInt(IReadOnlyDictionary<string, string> fields, string key)
    {
        var parsed = int.TryParse(
            fields.GetValueOrDefault(key)?.Trim(),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var value);

        // Zero is how these formats spell "not counted".
        return parsed && value > 0 ? value : null;
    }

    [GeneratedRegex(
        @"^(?<date>\d{4}-\d{2}-\d{2})(?:[T ](?<time>\d{2}:\d{2}(?::\d{2})?)(?:[.,](?<fraction>\d+))?)?\s*(?<zone>Z|[+-]\d{2}(?::?\d{2})?)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex IsoDate();

    [GeneratedRegex(
        @"^P(?:(?<days>\d+)D)?(?:T(?:(?<hours>\d+)H)?(?:(?<minutes>\d+)M)?(?:(?<seconds>\d+(?:\.\d+)?)S)?)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex IsoDuration();

    [GeneratedRegex(@"^([A-Za-z][\w .-]*?)/(\d[\w.]*)", RegexOptions.CultureInvariant)]
    private static partial Regex NameSlashVersion();

    [GeneratedRegex(@"\((?<name>[^()]+)\)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex PaperSizeName();
}
