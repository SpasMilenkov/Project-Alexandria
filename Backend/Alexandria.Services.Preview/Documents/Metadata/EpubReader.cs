using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Documents.Metadata;

/// <summary>
/// Reads an EPUB's package document (the OPF file): Dublin Core fields plus the <c>meta</c>
/// elements around them. EPUB 3 attaches roles, title types and series details through
/// <c>meta refines="#id"</c> elements, while EPUB 2 writes them as <c>opf:</c> attributes, so
/// both shapes are read. Copy protection is recognized but does not stop the read, because
/// stores leave the package document readable.
/// </summary>
internal static partial class EpubReader
{
    private const string DcNamespace = "http://purl.org/dc/elements/1.1/";
    private const string OpfNamespace = "http://www.idpf.org/2007/opf";
    private const string PackageMediaType = "application/oebps-package+xml";
    private const string AuthorRole = "aut";

    /// <summary>
    /// The package document lists every file in the book, so a large illustrated edition can
    /// run to a few hundred kilobytes. Past this it is broken or hostile.
    /// </summary>
    internal const long MaxPackageBytes = 4 * 1024 * 1024;

    // These scramble embedded fonts so they cannot be lifted out, but every reading app knows
    // how to undo them, so they are not copy protection.
    private static readonly HashSet<string> FontObfuscation = new(StringComparer.Ordinal)
    {
        "http://www.idpf.org/2008/embedding",
        "http://ns.adobe.com/pdf/enc#RC"
    };

    // Each scheme leaves its own license file next to the container.
    private static readonly (string Path, string Scheme)[] LicenseFiles =
    [
        ("META-INF/rights.xml", "adobe-adept"),
        ("META-INF/license.lcpl", "readium-lcp"),
        ("META-INF/sinf.xml", "apple-fairplay")
    ];

    public static DocumentFields Read(Stream source, ILogger logger)
    {
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        var fields = new DocumentFieldBag();

        fields.Add("package:drm", DrmScheme(archive, logger));

        var package = FindPackage(archive, logger);
        if (package is not null)
            ReadPackage(package, fields, logger);

        return fields.ToFields();
    }

    private static string? DrmScheme(ZipArchive archive, ILogger logger)
    {
        foreach (var (path, scheme) in LicenseFiles)
        {
            if (PackagePart.Find(archive, path) is not null)
                return scheme;
        }

        var encryption = PackagePart.Find(archive, "META-INF/encryption.xml");
        if (encryption is null)
            return null;

        try
        {
            var encrypted = PackagePart.Load(encryption)
                .Descendants()
                .Where(e => e.Name.LocalName == "EncryptionMethod")
                .Select(e => e.Attribute("Algorithm")?.Value)
                .Any(algorithm => algorithm is not null && !FontObfuscation.Contains(algorithm));

            return encrypted ? "unknown" : null;
        }
        catch (Exception ex) when (ex is XmlException or PartTooLargeException or InvalidDataException)
        {
            LogPartSkipped(logger, ex, encryption.FullName);
            return null;
        }
    }

    private static ZipArchiveEntry? FindPackage(ZipArchive archive, ILogger logger)
    {
        var container = PackagePart.Find(archive, "META-INF/container.xml");
        if (container is not null)
        {
            try
            {
                var path = PackagePart.Load(container)
                    .Descendants()
                    .Where(e => e.Name.LocalName == "rootfile")
                    .Where(e => (e.Attribute("media-type")?.Value ?? PackageMediaType) == PackageMediaType)
                    .Select(e => e.Attribute("full-path")?.Value)
                    .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));

                var entry = path is null ? null : PackagePart.Find(archive, path);
                if (entry is not null)
                    return entry;
            }
            catch (Exception ex) when (ex is XmlException or PartTooLargeException or InvalidDataException)
            {
                LogPartSkipped(logger, ex, container.FullName);
            }
        }

        // Some hand-built books skip the container or point it at the wrong path. A book has
        // exactly one package document, so the first one found is it.
        return archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase));
    }

    private static void ReadPackage(ZipArchiveEntry entry, DocumentFieldBag fields, ILogger logger)
    {
        try
        {
            var root = PackagePart.Load(entry, MaxPackageBytes).Root;
            if (root is null || root.Name.LocalName != "package")
                return;

            fields.Add("package:version", root.Attribute("version")?.Value);

            // OEBPS 1.x wrapped the fields in a dc-metadata element, so descendants are read
            // rather than direct children.
            var metadata = root.Elements().FirstOrDefault(e => e.Name.LocalName == "metadata");
            if (metadata is null)
                return;

            var elements = metadata.Descendants().Where(e => !e.HasElements).ToList();
            var refinements = Refinements(elements);

            foreach (var element in elements)
            {
                if (element.Name.NamespaceName == DcNamespace)
                    AddDublinCore(element, refinements, fields);
                else if (element.Name.LocalName == "meta")
                    AddMeta(element, refinements, fields);
            }
        }
        catch (Exception ex) when (ex is XmlException or PartTooLargeException or InvalidDataException)
        {
            LogPartSkipped(logger, ex, entry.FullName);
        }
    }

    /// <summary>
    /// EPUB 3 <c>meta</c> elements that describe another element, grouped by the id they refine.
    /// </summary>
    private static Dictionary<string, List<(string Property, string Value)>> Refinements(
        IEnumerable<XElement> elements)
    {
        var result = new Dictionary<string, List<(string, string)>>(StringComparer.Ordinal);

        foreach (var element in elements)
        {
            var refines = element.Attribute("refines")?.Value;
            var property = element.Attribute("property")?.Value;
            if (element.Name.LocalName != "meta" || refines is null || property is null)
                continue;

            var id = refines.TrimStart('#');
            if (!result.TryGetValue(id, out var list))
            {
                list = [];
                result[id] = list;
            }

            list.Add((property.Trim(), element.Value.Trim()));
        }

        return result;
    }

    private static List<string> Refined(
        XElement element,
        Dictionary<string, List<(string Property, string Value)>> refinements,
        string property)
    {
        var id = element.Attribute("id")?.Value;
        if (id is null || !refinements.TryGetValue(id, out var list))
            return [];

        return list.Where(r => r.Property == property && r.Value.Length > 0).Select(r => r.Value).ToList();
    }

    private static void AddDublinCore(
        XElement element,
        Dictionary<string, List<(string Property, string Value)>> refinements,
        DocumentFieldBag fields)
    {
        var name = element.Name.LocalName;
        var value = element.Value;

        switch (name)
        {
            case "title":
                AddTitle(element, refinements, fields);
                return;

            case "creator":
            case "contributor":
                AddPerson(element, refinements, fields);
                return;

            case "date":
            {
                var evt = element.Attribute(XName.Get("event", OpfNamespace))?.Value.Trim().ToLowerInvariant();
                fields.Add(string.IsNullOrEmpty(evt) ? "opf:date" : $"opf:date:{evt}", value);
                return;
            }

            case "identifier":
                fields.Add("opf:identifier", value);
                if (IsIsbnIdentifier(element, refinements, value))
                    fields.Add("opf:isbn", value);
                return;

            default:
                fields.Add($"opf:{name}", value);
                return;
        }
    }

    // A book can carry its full title, a short one and a subtitle. The main one is marked in
    // EPUB 3, and otherwise it is the first, which is what reading apps show.
    private static void AddTitle(
        XElement element,
        Dictionary<string, List<(string Property, string Value)>> refinements,
        DocumentFieldBag fields)
    {
        var type = Refined(element, refinements, "title-type").FirstOrDefault()?.ToLowerInvariant();

        if (type is null or "main" && !fields.Contains("opf:title"))
        {
            fields.Add("opf:title", element.Value);
            return;
        }

        fields.Add($"opf:title:{type ?? "alternate"}", element.Value);
    }

    // Creators without a role are authors by the spec's default. Contributors are kept by role
    // (translator, illustrator...) so a later column can pick them up from the raw fields.
    private static void AddPerson(
        XElement element,
        Dictionary<string, List<(string Property, string Value)>> refinements,
        DocumentFieldBag fields)
    {
        var roles = Refined(element, refinements, "role");
        var legacyRole = element.Attribute(XName.Get("role", OpfNamespace))?.Value;
        if (!string.IsNullOrWhiteSpace(legacyRole))
            roles.Add(legacyRole);

        var normalized = roles.Select(r => r.Trim().ToLowerInvariant()).Distinct().ToList();
        var isCreator = element.Name.LocalName == "creator";

        if (normalized.Contains(AuthorRole) || (isCreator && normalized.Count == 0))
        {
            fields.Add("opf:creator", element.Value);
            return;
        }

        var prefix = isCreator ? "opf:creator" : "opf:contributor";
        if (normalized.Count == 0)
        {
            fields.Add(prefix, element.Value);
            return;
        }

        foreach (var role in normalized)
            fields.Add($"{prefix}:{role}", element.Value);
    }

    private static bool IsIsbnIdentifier(
        XElement element,
        Dictionary<string, List<(string Property, string Value)>> refinements,
        string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("urn:isbn:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("isbn:", StringComparison.OrdinalIgnoreCase))
            return true;

        var scheme = element.Attribute(XName.Get("scheme", OpfNamespace))?.Value;
        if (string.Equals(scheme?.Trim(), "ISBN", StringComparison.OrdinalIgnoreCase))
            return true;

        // ONIX code list 5 numbers its identifier kinds: 02 is ISBN-10 and 15 is ISBN-13.
        return Refined(element, refinements, "identifier-type").Any(t => t.Trim() is "02" or "15");
    }

    private static void AddMeta(
        XElement element,
        Dictionary<string, List<(string Property, string Value)>> refinements,
        DocumentFieldBag fields)
    {
        // EPUB 2 writes name/content pairs, which is also where calibre keeps its series.
        var legacyName = element.Attribute("name")?.Value;
        if (!string.IsNullOrWhiteSpace(legacyName))
        {
            fields.Add($"opf-meta:{legacyName.Trim()}", element.Attribute("content")?.Value);
            return;
        }

        var property = element.Attribute("property")?.Value;
        if (string.IsNullOrWhiteSpace(property) || element.Attribute("refines") is not null)
            return;

        property = property.Trim();

        if (property == "belongs-to-collection")
        {
            AddCollection(element, refinements, fields);
            return;
        }

        fields.Add($"opf-meta:{property}", element.Value);
    }

    // Collections are also used for reading lists ("set") and anthologies, which say nothing
    // about which series a book belongs to, so only an explicit series counts.
    private static void AddCollection(
        XElement element,
        Dictionary<string, List<(string Property, string Value)>> refinements,
        DocumentFieldBag fields)
    {
        var type = Refined(element, refinements, "collection-type").FirstOrDefault()?.Trim().ToLowerInvariant();

        if (type == "series" && !fields.Contains("opf:series"))
        {
            fields.Add("opf:series", element.Value);
            fields.Add("opf:series-index", Refined(element, refinements, "group-position").FirstOrDefault());
            return;
        }

        fields.Add($"opf:collection:{type ?? "unspecified"}", element.Value);
    }

    [LoggerMessage(4275, LogLevel.Warning, "Skipped unreadable EPUB part {PartName}")]
    private static partial void LogPartSkipped(ILogger logger, Exception ex, string partName);
}
