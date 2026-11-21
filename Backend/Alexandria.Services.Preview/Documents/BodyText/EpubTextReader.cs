using System.IO.Compression;
using System.Text;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Preview.Documents.Metadata;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Documents.BodyText;

/// <summary>
/// Reads an EPUB's chapters in reading order: the package document's spine lists them, and its
/// manifest maps each entry to a file. A book whose spine cannot be resolved falls back to every
/// (X)HTML file in name order, which is how most tools number chapters.
/// </summary>
internal static class EpubTextReader
{
    /// <summary>
    /// A chapter is rarely more than a few hundred kilobytes; past this it is broken or hostile.
    /// </summary>
    internal const long MaxChapterBytes = 16 * 1024 * 1024;

    private static readonly HashSet<string> ChapterMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/xhtml+xml", "text/html"
    };

    private static readonly string[] ChapterExtensions = [".xhtml", ".html", ".htm"];

    public static DocumentTextStatus? Read(Stream source, TextCollector collector, ILogger logger)
    {
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

        // The package document stays readable under copy protection, but the chapters do not.
        if (EpubReader.DrmScheme(archive, logger) is not null)
            return DocumentTextStatus.CopyProtected;

        var package = EpubReader.FindPackage(archive, logger)
                      ?? throw new InvalidDataException("The book has no package document.");

        var chapters = SpineChapters(archive, package);
        if (chapters.Count == 0)
            chapters = FallbackChapters(archive);

        foreach (var chapter in chapters)
        {
            if (collector.IsFull)
                break;

            ReadChapter(chapter, collector, logger);
            collector.LineBreak();
        }

        return null;
    }

    /// <exception cref="System.Xml.XmlException">The package document is not well-formed XML.</exception>
    private static List<ZipArchiveEntry> SpineChapters(ZipArchive archive, ZipArchiveEntry package)
    {
        var root = PackagePart.Load(package, EpubReader.MaxPackageBytes).Root;
        if (root is null)
            return [];

        var directory = Path.GetDirectoryName(package.FullName)?.Replace('\\', '/') ?? "";

        var manifest = root.Descendants()
            .Where(e => e.Name.LocalName == "item")
            .Select(e => (Id: e.Attribute("id")?.Value, Href: e.Attribute("href")?.Value,
                MediaType: e.Attribute("media-type")?.Value))
            .Where(i => i.Id is not null && i.Href is not null)
            .GroupBy(i => i.Id!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var chapters = new List<ZipArchiveEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var spine = root.Descendants()
            .Where(e => e.Name.LocalName == "itemref")
            .Select(e => e.Attribute("idref")?.Value);

        foreach (var idref in spine)
        {
            if (idref is null || !manifest.TryGetValue(idref, out var item))
                continue;

            if (!IsChapter(item.MediaType, item.Href!))
                continue;

            var path = TextPackagePart.ResolveRelative(directory, HrefPath(item.Href!));
            var entry = PackagePart.Find(archive, path);

            if (entry is not null && seen.Add(entry.FullName))
                chapters.Add(entry);
        }

        return chapters;
    }

    private static List<ZipArchiveEntry> FallbackChapters(ZipArchive archive)
        => archive.Entries
            .Where(e => IsChapter(mediaType: null, e.FullName))
            .OrderBy(e => e.FullName, StringComparer.Ordinal)
            .ToList();

    private static bool IsChapter(string? mediaType, string href)
    {
        if (mediaType is not null)
            return ChapterMediaTypes.Contains(mediaType);

        return ChapterExtensions.Any(ext => href.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    // Manifest links are URLs: spaces arrive as %20, and a fragment points inside the file.
    private static string HrefPath(string href)
    {
        var fragment = href.IndexOf('#');
        var path = fragment >= 0 ? href[..fragment] : href;

        return Uri.UnescapeDataString(path);
    }

    private static void ReadChapter(ZipArchiveEntry chapter, TextCollector collector, ILogger logger)
    {
        string html;

        try
        {
            using var stream = new LimitedReadStream(chapter.Open(), MaxChapterBytes, chapter.FullName);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            html = reader.ReadToEnd();
        }
        catch (PartTooLargeException)
        {
            collector.MarkTruncated();
            BodyTextLog.PartTooLarge(logger, chapter.FullName, MaxChapterBytes);
            return;
        }
        catch (InvalidDataException ex)
        {
            BodyTextLog.PartSkipped(logger, ex, chapter.FullName);
            return;
        }

        HtmlTextStripper.Strip(html, collector);
    }
}