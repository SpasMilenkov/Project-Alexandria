using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Preview.Documents.Metadata;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Documents.BodyText;

/// <summary>
/// Reads the body text of docx, pptx and xlsx packages straight from their XML parts. Each
/// method returns a status only when the package could not be read at all; otherwise the text is
/// in the collector.
/// </summary>
internal static partial class OfficeOpenXmlTextReader
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string OfficeDocumentSuffix = "/officeDocument";
    private const string NotesSlideSuffix = "/notesSlide";

    // Shapes are stored twice, once for current Office and once as a fallback for old readers,
    // so the fallback copy is skipped or every text box would be indexed twice.
    private static readonly XmlTextRules WordRules = new()
    {
        TextContainers = new HashSet<XName> { W + "t" },
        Spaces = new HashSet<XName> { W + "tab", W + "ptab" },
        LineBreaks = new HashSet<XName> { W + "br", W + "cr" },
        LineEnds = new HashSet<XName> { W + "p" },
        Skipped = new HashSet<XName> { Mc + "Fallback" }
    };

    // Fields hold slide numbers and dates that are filled in on screen, not words of the deck.
    private static readonly XmlTextRules DrawingRules = new()
    {
        TextContainers = new HashSet<XName> { A + "t" },
        LineBreaks = new HashSet<XName> { A + "br" },
        LineEnds = new HashSet<XName> { A + "p" },
        Skipped = new HashSet<XName> { Mc + "Fallback", A + "fld" }
    };

    public static DocumentTextStatus? ReadWordprocessing(Stream source, TextCollector collector, ILogger logger)
    {
        if (OfficeOpenXmlReader.StartsWithCompoundFileSignature(source))
            return DocumentTextStatus.PasswordProtected;

        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

        var body = PackagePart.Find(archive, MainDocumentPath(archive, logger) ?? "word/document.xml")
                   ?? throw new InvalidDataException("The package has no main document part.");

        TextPackagePart.Read(body, WordRules, collector, isBody: true, logger);

        var secondary = archive.Entries
            .Where(e => WordSecondaryPart().IsMatch(e.FullName))
            .OrderBy(e => e.FullName, StringComparer.Ordinal);

        foreach (var part in secondary)
        {
            if (collector.IsFull)
                break;

            collector.LineBreak();
            TextPackagePart.Read(part, WordRules, collector, isBody: false, logger);
        }

        return null;
    }

    /// <summary>
    /// One page per slide, in slide order, each followed by its speaker notes, so the n-th page
    /// of the text is the n-th slide.
    /// </summary>
    public static DocumentTextStatus? ReadPresentation(Stream source, TextCollector collector, ILogger logger)
    {
        if (OfficeOpenXmlReader.StartsWithCompoundFileSignature(source))
            return DocumentTextStatus.PasswordProtected;

        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

        foreach (var slide in NumberedParts(archive, SlidePart()))
        {
            if (collector.IsFull)
                break;

            TextPackagePart.Read(slide, DrawingRules, collector, isBody: true, logger);

            var notes = NotesFor(archive, slide, logger);
            if (notes is not null)
            {
                collector.LineBreak();
                TextPackagePart.Read(notes, DrawingRules, collector, isBody: false, logger);
            }

            collector.PageBreak();
        }

        return null;
    }

    /// <summary>
    /// One page per sheet, one line per row, cells separated by spaces. Numbers are kept, since an
    /// invoice or order number is often exactly what someone searches for.
    /// </summary>
    public static DocumentTextStatus? ReadSpreadsheet(Stream source, TextCollector collector, ILogger logger)
    {
        if (OfficeOpenXmlReader.StartsWithCompoundFileSignature(source))
            return DocumentTextStatus.PasswordProtected;

        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

        var sharedStrings = ReadSharedStrings(archive, collector, logger);

        foreach (var sheet in NumberedParts(archive, SheetPart()))
        {
            if (collector.IsFull)
                break;

            ReadSheet(sheet, sharedStrings, collector, logger);
            collector.PageBreak();
        }

        return null;
    }

    private static string? MainDocumentPath(ZipArchive archive, ILogger logger)
    {
        var rels = PackagePart.Find(archive, "_rels/.rels");
        if (rels is null)
            return null;

        return RelationshipTarget(rels, OfficeDocumentSuffix, logger);
    }

    private static ZipArchiveEntry? NotesFor(ZipArchive archive, ZipArchiveEntry slide, ILogger logger)
    {
        var directory = Path.GetDirectoryName(slide.FullName)?.Replace('\\', '/') ?? "";
        var rels = PackagePart.Find(archive, $"{directory}/_rels/{Path.GetFileName(slide.FullName)}.rels");
        if (rels is null)
            return null;

        var target = RelationshipTarget(rels, NotesSlideSuffix, logger);
        if (target is null)
            return null;

        return PackagePart.Find(archive, TextPackagePart.ResolveRelative(directory, target));
    }

    // Writers differ on the exact type URI (LibreOffice lowercases "officedocument"), so the type
    // is matched on its stable suffix, as the property reader does.
    private static string? RelationshipTarget(ZipArchiveEntry rels, string typeSuffix, ILogger logger)
    {
        try
        {
            return PackagePart.Load(rels).Root?.Elements()
                .Where(r => (r.Attribute("Type")?.Value ?? "").EndsWith(typeSuffix, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Attribute("Target")?.Value)
                .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        }
        catch (Exception ex) when (ex is XmlException or PartTooLargeException or InvalidDataException)
        {
            BodyTextLog.PartSkipped(logger, ex, rels.FullName);
            return null;
        }
    }

    // Parts are numbered without padding, so "slide10" has to sort after "slide9".
    private static IEnumerable<ZipArchiveEntry> NumberedParts(ZipArchive archive, Regex pattern)
        => archive.Entries
            .Select(e => (Entry: e, Match: pattern.Match(e.FullName)))
            .Where(p => p.Match.Success)
            .OrderBy(p => int.Parse(p.Match.Groups[1].ValueSpan, CultureInfo.InvariantCulture))
            .Select(p => p.Entry);

    /// <summary>
    /// The workbook's string table, which cells point into by index. Phonetic guides (rPh) are
    /// left out, because they repeat the cell's text in another script.
    /// </summary>
    private static List<string> ReadSharedStrings(ZipArchive archive, TextCollector collector, ILogger logger)
    {
        var strings = new List<string>();

        var entry = PackagePart.Find(archive, "xl/sharedStrings.xml");
        if (entry is null)
            return strings;

        var total = 0L;

        try
        {
            using var reader = PackagePart.OpenXml(entry, TextPackagePart.MaxBodyPartBytes, XmlTextWalker.Settings);
            var current = new StringBuilder();
            var inPhonetic = 0;
            var inText = false;

            while (reader.Read())
            {
                if (reader.NamespaceURI != SpreadsheetNamespace &&
                    reader.NodeType is XmlNodeType.Element or XmlNodeType.EndElement)
                    continue;

                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.LocalName == "si")
                        current.Clear();
                    else if (reader.LocalName == "rPh" && !reader.IsEmptyElement)
                        inPhonetic++;
                    else if (reader.LocalName == "t" && !reader.IsEmptyElement)
                        inText = true;

                    continue;
                }

                if (reader.NodeType == XmlNodeType.EndElement)
                {
                    if (reader.LocalName == "si")
                    {
                        strings.Add(current.ToString());
                        total += current.Length;
                    }
                    else if (reader.LocalName == "rPh" && inPhonetic > 0)
                    {
                        inPhonetic--;
                    }
                    else if (reader.LocalName == "t")
                    {
                        inText = false;
                    }

                    continue;
                }

                if (inText && inPhonetic == 0 && reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA
                        or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)
                    current.Append(reader.Value);

                // Strings past the stored limit could never make it into the text anyway.
                if (total > DocumentTextExtractor.MaxStoredCharacters)
                {
                    collector.MarkTruncated();
                    break;
                }
            }
        }
        catch (PartTooLargeException)
        {
            collector.MarkTruncated();
            BodyTextLog.PartTooLarge(logger, entry.FullName, TextPackagePart.MaxBodyPartBytes);
        }

        return strings;
    }

    private static void ReadSheet(ZipArchiveEntry sheet, List<string> sharedStrings, TextCollector collector,
        ILogger logger)
    {
        try
        {
            using var reader = PackagePart.OpenXml(sheet, TextPackagePart.MaxBodyPartBytes, XmlTextWalker.Settings);
            var cellType = "";
            var value = new StringBuilder();
            var inValue = false;

            while (!collector.IsFull && reader.Read())
            {
                if (reader.NodeType is XmlNodeType.Element or XmlNodeType.EndElement
                    && reader.NamespaceURI != SpreadsheetNamespace)
                    continue;

                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.LocalName == "c")
                    {
                        cellType = reader.GetAttribute("t") ?? "";
                        value.Clear();
                    }
                    else if (reader.LocalName is "v" or "t" && !reader.IsEmptyElement)
                    {
                        inValue = true;
                    }

                    continue;
                }

                if (reader.NodeType == XmlNodeType.EndElement)
                {
                    if (reader.LocalName is "v" or "t")
                    {
                        inValue = false;
                    }
                    else if (reader.LocalName == "c")
                    {
                        // A line break typed inside a cell would otherwise split the row.
                        collector.Append(CellText(cellType, value.ToString(), sharedStrings), inline: true);
                        collector.Space();
                    }
                    else if (reader.LocalName == "row")
                    {
                        collector.LineBreak();
                    }

                    continue;
                }

                if (inValue)
                    value.Append(reader.Value);
            }
        }
        catch (PartTooLargeException)
        {
            collector.MarkTruncated();
            BodyTextLog.PartTooLarge(logger, sheet.FullName, TextPackagePart.MaxBodyPartBytes);
        }
    }

    // Booleans and error codes ("#DIV/0!") are not words anyone looks for.
    internal static string? CellText(string cellType, string value, List<string> sharedStrings)
    {
        if (cellType == "s")
        {
            var isIndex = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var index);

            return isIndex && index < sharedStrings.Count ? sharedStrings[index] : null;
        }

        if (cellType is "b" or "e")
            return null;

        return value;
    }

    [GeneratedRegex(@"^word/(footnotes|endnotes|header\d*|footer\d*)\.xml$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WordSecondaryPart();

    [GeneratedRegex(@"^ppt/slides/slide(\d+)\.xml$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SlidePart();

    [GeneratedRegex(@"^xl/worksheets/sheet(\d+)\.xml$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SheetPart();
}