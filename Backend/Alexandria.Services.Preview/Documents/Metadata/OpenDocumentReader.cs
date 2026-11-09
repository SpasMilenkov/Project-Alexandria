using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Documents.Metadata;

/// <summary>
/// Reads odt, ods and odp properties from <c>meta.xml</c>, plus a slide count from the content
/// part for presentations, whose statistics leave it out.
/// </summary>
internal static partial class OpenDocumentReader
{
    private const string MetaNamespace = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";
    private const string ManifestNamespace = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
    private const string DrawNamespace = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";

    /// <summary>
    /// The content part holds the whole body, so it gets more room than a property part, and
    /// a presentation past this just goes without a slide count.
    /// </summary>
    internal const long MaxContentPartBytes = 32 * 1024 * 1024;

    public static DocumentFields Read(Stream source, bool isPresentation, ILogger logger)
    {
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        var fields = new DocumentFieldBag();

        // An encrypted package keeps its manifest readable but scrambles the other parts.
        if (IsEncrypted(archive, logger))
            return DocumentFields.PasswordProtected;

        ReadMeta(archive, fields, logger);

        if (isPresentation)
            CountSlides(archive, fields, logger);

        return fields.ToFields();
    }

    private static bool IsEncrypted(ZipArchive archive, ILogger logger)
    {
        var manifest = PackagePart.Find(archive, "META-INF/manifest.xml");
        if (manifest is null)
            return false;

        try
        {
            return PackagePart.Load(manifest)
                .Descendants()
                .Any(e => e.Name.LocalName == "encryption-data" && e.Name.NamespaceName == ManifestNamespace);
        }
        catch (Exception ex) when (ex is XmlException or PartTooLargeException or InvalidDataException)
        {
            LogPartSkipped(logger, ex, manifest.FullName);
            return false;
        }
    }

    private static void ReadMeta(ZipArchive archive, DocumentFieldBag fields, ILogger logger)
    {
        var entry = PackagePart.Find(archive, "meta.xml");
        if (entry is null)
            return;

        try
        {
            var meta = PackagePart.Load(entry)
                .Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "meta");
            if (meta is null)
                return;

            foreach (var element in meta.Elements())
            {
                var name = element.Name.LocalName;

                if (name == "document-statistic")
                {
                    foreach (var attribute in element.Attributes())
                        fields.Add($"odf-stat:{attribute.Name.LocalName}", attribute.Value);

                    continue;
                }

                if (element.HasElements)
                    continue;

                // Custom properties all share one element name and differ only in this attribute.
                if (name == "user-defined")
                {
                    var userName = element.Attribute(XName.Get("name", MetaNamespace))?.Value;
                    if (!string.IsNullOrWhiteSpace(userName))
                        fields.Add($"odf-user:{userName.Trim()}", element.Value);

                    continue;
                }

                fields.Add($"odf:{name}", element.Value);
            }
        }
        catch (Exception ex) when (ex is XmlException or PartTooLargeException or InvalidDataException)
        {
            LogPartSkipped(logger, ex, entry.FullName);
        }
    }

    private static void CountSlides(ZipArchive archive, DocumentFieldBag fields, ILogger logger)
    {
        var entry = PackagePart.Find(archive, "content.xml");
        if (entry is null)
            return;

        try
        {
            using var reader = PackagePart.OpenXml(entry, MaxContentPartBytes);
            var slides = 0;

            while (reader.Read())
            {
                if (reader is { NodeType: XmlNodeType.Element, LocalName: "page", NamespaceURI: DrawNamespace })
                    slides++;
            }

            if (slides > 0)
                fields.Add("package:pages", slides.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is XmlException or PartTooLargeException or InvalidDataException)
        {
            LogPartSkipped(logger, ex, entry.FullName);
        }
    }

    [LoggerMessage(4271, LogLevel.Warning, "Skipped unreadable document part {PartName}")]
    private static partial void LogPartSkipped(ILogger logger, Exception ex, string partName);
}
