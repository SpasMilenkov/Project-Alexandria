using Alexandria.Common.Mime;

namespace Alexandria.Common.Helpers;

/// <summary>
/// The document types whose properties can be read. Legacy binary Office files and RTF are left
/// out: they keep properties in formats no reader here understands. Books have no preview job, so
/// only the backfill sweep reads them.
/// </summary>
public static class DocumentMetadataFileTypes
{
    private static readonly Dictionary<string, DocumentMetadataFormat> Formats = new(StringComparer.Ordinal)
    {
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = DocumentMetadataFormat.OfficeOpenXml,
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = DocumentMetadataFormat.OfficeOpenXml,
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = DocumentMetadataFormat.OfficeOpenXml,
        ["application/vnd.oasis.opendocument.text"] = DocumentMetadataFormat.OpenDocument,
        ["application/vnd.oasis.opendocument.spreadsheet"] = DocumentMetadataFormat.OpenDocument,
        ["application/vnd.oasis.opendocument.presentation"] = DocumentMetadataFormat.OpenDocument,
        ["application/pdf"] = DocumentMetadataFormat.Pdf,
        ["application/epub+zip"] = DocumentMetadataFormat.Epub,
        ["application/x-mobipocket-ebook"] = DocumentMetadataFormat.Mobi,
        ["application/vnd.amazon.mobi8-ebook"] = DocumentMetadataFormat.Mobi,
        ["application/vnd.amazon.ebook"] = DocumentMetadataFormat.Mobi
    };

    /// <summary>The supported set, also exported to the frontend catalog.</summary>
    public static MimeMatcher Supported { get; } = new(types: Formats.Keys);

    /// <summary>
    /// The ebook types, also exported to the frontend catalog, which words their details as a
    /// book's rather than a document's.
    /// </summary>
    public static MimeMatcher Books { get; } = new(types: Formats
        .Where(pair => pair.Value is DocumentMetadataFormat.Epub or DocumentMetadataFormat.Mobi)
        .Select(pair => pair.Key));

    /// <summary>
    /// The reader format for a MIME type, or null when its properties cannot be read. The type is
    /// canonicalized first, so aliases resolve the same way they do everywhere else.
    /// </summary>
    public static DocumentMetadataFormat? FormatOf(string? mimeType)
    {
        var canonical = MimeAliases.Canonicalize(mimeType);

        return Formats.TryGetValue(canonical, out var format) ? format : null;
    }

    public static bool IsBook(string? mimeType) => Books.Matches(MimeAliases.Canonicalize(mimeType));
}
