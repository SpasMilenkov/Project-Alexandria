using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Alexandria.Services.Preview.Documents.Metadata;

/// <summary>
/// Safe access to the XML parts inside Office Open XML and OpenDocument packages. A part's
/// declared size is written by whoever made the file, so reads are capped by counting the
/// bytes actually inflated rather than trusting it.
/// </summary>
internal static class PackagePart
{
    /// <summary>
    /// Property parts are a few kilobytes, so anything this large is broken or hostile.
    /// </summary>
    internal const long MaxPropertyPartBytes = 1024 * 1024;

    // A DTD can pull in external files or expand entities without bound, and no real package
    // part has one, so a document type declaration fails the read.
    internal static XmlReaderSettings XmlSettings { get; } = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true,
        CloseInput = true
    };

    /// <summary>
    /// The entry at <paramref name="path"/>. Paths inside a package are case-sensitive by spec,
    /// but some writers disagree with their own relationship targets, so an exact miss falls
    /// back to a case-insensitive match.
    /// </summary>
    public static ZipArchiveEntry? Find(ZipArchive archive, string path)
    {
        var normalized = path.TrimStart('/');

        return archive.GetEntry(normalized)
               ?? archive.Entries.FirstOrDefault(e =>
                   string.Equals(e.FullName, normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <exception cref="PartTooLargeException">The part inflates past <paramref name="maxBytes"/>.</exception>
    /// <exception cref="XmlException">The part is not well-formed XML.</exception>
    public static XDocument Load(ZipArchiveEntry entry, long maxBytes = MaxPropertyPartBytes)
    {
        using var reader = OpenXml(entry, maxBytes);

        return XDocument.Load(reader);
    }

    /// <inheritdoc cref="Load"/>
    public static XmlReader OpenXml(ZipArchiveEntry entry, long maxBytes)
    {
        var stream = new LimitedReadStream(entry.Open(), maxBytes, entry.FullName);

        return XmlReader.Create(stream, XmlSettings);
    }
}
