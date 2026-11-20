using System.IO.Compression;
using System.Xml.Linq;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Preview.Documents.Metadata;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Documents.BodyText;

/// <summary>
/// Reads the body text of odt, ods and odp packages from <c>content.xml</c>. Slides and sheets
/// each end a page, so the n-th page of the text is the n-th slide or sheet.
/// </summary>
internal static class OpenDocumentTextReader
{
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";

    private static readonly HashSet<XName> Containers = [Text + "p", Text + "h"];
    private static readonly HashSet<XName> SpaceMarkers = [Text + "s", Text + "tab"];
    private static readonly HashSet<XName> LineMarkers = [Text + "line-break"];

    // Comments are reviewers' notes rather than the document, tracked changes hold deleted text,
    // and a note citation is just the footnote's number.
    private static readonly HashSet<XName> Ignored =
    [
        Office + "annotation", Office + "annotation-end", Text + "tracked-changes", Text + "note-citation"
    ];

    private static readonly XmlTextRules DocumentRules = new()
    {
        TextContainers = Containers,
        Spaces = SpaceMarkers,
        LineBreaks = LineMarkers,
        LineEnds = Containers,
        PageEnds = new HashSet<XName> { Draw + "page", Table + "table" },
        Skipped = Ignored
    };

    // Every cell holds its own paragraph, so paragraphs only separate words here and rows end
    // the lines, matching how xlsx sheets read.
    private static readonly XmlTextRules SpreadsheetRules = new()
    {
        TextContainers = Containers,
        Spaces = SpaceMarkers,
        LineBreaks = LineMarkers,
        SpaceEnds = new HashSet<XName> { Text + "p", Text + "h", Table + "table-cell" },
        LineEnds = new HashSet<XName> { Table + "table-row" },
        PageEnds = new HashSet<XName> { Table + "table" },
        Skipped = Ignored
    };

    public static DocumentTextStatus? Read(Stream source, TextCollector collector, ILogger logger,
        bool isSpreadsheet = false)
    {
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

        // An encrypted package keeps its manifest readable but scrambles the other parts.
        if (OpenDocumentReader.IsEncrypted(archive, logger))
            return DocumentTextStatus.PasswordProtected;

        var content = PackagePart.Find(archive, "content.xml")
                      ?? throw new InvalidDataException("The package has no content part.");

        var rules = isSpreadsheet ? SpreadsheetRules : DocumentRules;
        TextPackagePart.Read(content, rules, collector, isBody: true, logger);

        return null;
    }
}