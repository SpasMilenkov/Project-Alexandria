using System.IO.Compression;
using System.Xml;
using Alexandria.Services.Preview.Documents.Metadata;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Documents.BodyText;

/// <summary>
/// Reads one body part of an Office or OpenDocument package into the collector, with the same
/// failure policy for every reader.
/// </summary>
internal static class TextPackagePart
{
    /// <summary>
    /// Bodies are far larger than property parts, but a part inflating past this is a zip bomb
    /// or a file whose text would be cut at the stored limit long before the end anyway.
    /// </summary>
    internal const long MaxBodyPartBytes = 128 * 1024 * 1024;

    /// <summary>
    /// An oversized part keeps the text read so far and marks the result truncated. A broken
    /// part is skipped when it is secondary (footnotes, notes), and fails the read when it is the
    /// body itself, since the document then has nothing trustworthy to index.
    /// </summary>
    public static void Read(
        ZipArchiveEntry entry,
        XmlTextRules rules,
        TextCollector collector,
        bool isBody,
        ILogger logger,
        long maxBytes = MaxBodyPartBytes)
    {
        try
        {
            using var reader = PackagePart.OpenXml(entry, maxBytes, XmlTextWalker.Settings);
            XmlTextWalker.Read(reader, rules, collector);
        }
        catch (PartTooLargeException)
        {
            collector.MarkTruncated();
            BodyTextLog.PartTooLarge(logger, entry.FullName, maxBytes);
        }
        catch (Exception ex) when (!isBody && ex is XmlException or InvalidDataException)
        {
            BodyTextLog.PartSkipped(logger, ex, entry.FullName);
        }
    }

    /// <summary>
    /// A path inside the package from a link relative to <paramref name="directory"/>. Office
    /// relationships and EPUB manifests both link this way, and may climb out with "..".
    /// </summary>
    public static string ResolveRelative(string directory, string target)
    {
        if (target.StartsWith('/'))
            return target.TrimStart('/');

        var segments = directory.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

        foreach (var segment in target.Split('/'))
        {
            if (segment == "..")
            {
                if (segments.Count > 0)
                    segments.RemoveAt(segments.Count - 1);

                continue;
            }

            if (segment is not ("." or ""))
                segments.Add(segment);
        }

        return string.Join('/', segments);
    }
}