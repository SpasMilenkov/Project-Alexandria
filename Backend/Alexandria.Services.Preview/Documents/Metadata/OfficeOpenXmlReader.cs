using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Documents.Metadata;

/// <summary>
/// Reads docx, xlsx and pptx properties: the core part (title, author, dates), the extended
/// part (app, company, counts) and a count of slide and sheet parts for files without one.
/// </summary>
internal static partial class OfficeOpenXmlReader
{
    private const string CorePropertiesSuffix = "/metadata/core-properties";
    private const string ExtendedPropertiesSuffix = "/extended-properties";
    private const string DefaultCorePath = "docProps/core.xml";
    private const string DefaultExtendedPath = "docProps/app.xml";

    // Office wraps a password-protected package in a compound file, so a "docx" with this
    // signature is encrypted rather than broken.
    private static readonly byte[] CompoundFileSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    public static DocumentFields Read(Stream source, ILogger logger)
    {
        if (StartsWithCompoundFileSignature(source))
            return DocumentFields.PasswordProtected;

        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        var fields = new DocumentFieldBag();
        var (corePath, extendedPath) = ResolvePropertyParts(archive, logger);

        ReadSimpleElements(archive, corePath, "core", fields, logger);
        ReadSimpleElements(archive, extendedPath, "app", fields, logger);
        CountParts(archive, fields);

        return fields.ToFields();
    }

    private static bool StartsWithCompoundFileSignature(Stream source)
    {
        Span<byte> header = stackalloc byte[CompoundFileSignature.Length];
        var read = source.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        source.Position = 0;

        return read == header.Length && header.SequenceEqual(CompoundFileSignature);
    }

    // The package relationships name the property parts. Writers differ on the exact type URI
    // (LibreOffice lowercases "officedocument"), so the type is matched on its stable suffix.
    private static (string CorePath, string ExtendedPath) ResolvePropertyParts(ZipArchive archive, ILogger logger)
    {
        var corePath = DefaultCorePath;
        var extendedPath = DefaultExtendedPath;

        var rels = PackagePart.Find(archive, "_rels/.rels");
        if (rels is null)
            return (corePath, extendedPath);

        try
        {
            var relationships = PackagePart.Load(rels).Root?.Elements() ?? [];

            foreach (var relationship in relationships)
            {
                var type = relationship.Attribute("Type")?.Value ?? "";
                var target = relationship.Attribute("Target")?.Value;
                if (string.IsNullOrWhiteSpace(target))
                    continue;

                if (type.EndsWith(CorePropertiesSuffix, StringComparison.OrdinalIgnoreCase))
                    corePath = target;
                else if (type.EndsWith(ExtendedPropertiesSuffix, StringComparison.OrdinalIgnoreCase))
                    extendedPath = target;
            }
        }
        catch (Exception ex) when (ex is XmlException or PartTooLargeException or InvalidDataException)
        {
            LogPartSkipped(logger, ex, rels.FullName);
        }

        return (corePath, extendedPath);
    }

    // Both property parts are flat lists of named values. Elements with children, like the
    // extended part's TitlesOfParts vectors, are skipped rather than flattened.
    private static void ReadSimpleElements(
        ZipArchive archive,
        string path,
        string prefix,
        DocumentFieldBag fields,
        ILogger logger)
    {
        var entry = PackagePart.Find(archive, path);
        if (entry is null)
            return;

        try
        {
            var root = PackagePart.Load(entry).Root;
            if (root is null)
                return;

            foreach (var element in root.Elements().Where(e => !e.HasElements))
                fields.Add($"{prefix}:{element.Name.LocalName}", element.Value);
        }
        catch (Exception ex) when (ex is XmlException or PartTooLargeException or InvalidDataException)
        {
            LogPartSkipped(logger, ex, entry.FullName);
        }
    }

    // Files from generators that skip the extended part still have one part per slide or sheet.
    private static void CountParts(ZipArchive archive, DocumentFieldBag fields)
    {
        var slides = archive.Entries.Count(e => SlidePart().IsMatch(e.FullName));
        var sheets = archive.Entries.Count(e => SheetPart().IsMatch(e.FullName));

        if (slides > 0)
            fields.Add("package:slides", slides.ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (sheets > 0)
            fields.Add("package:sheets", sheets.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [GeneratedRegex(@"^ppt/slides/slide\d+\.xml$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SlidePart();

    [GeneratedRegex(@"^xl/worksheets/sheet\d+\.xml$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SheetPart();

    [LoggerMessage(4270, LogLevel.Warning, "Skipped unreadable document part {PartName}")]
    private static partial void LogPartSkipped(ILogger logger, Exception ex, string partName);
}
