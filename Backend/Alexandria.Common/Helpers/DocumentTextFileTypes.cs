using Alexandria.Common.Mime;

namespace Alexandria.Common.Helpers;

/// <summary>
/// The types whose body text is read for full-text search. Legacy binary Office files, RTF and
/// MOBI are left out: each needs a converter or a decompressor the worker does not have. Source
/// code and data files (CSV, JSON) stay out too, because their tokens would crowd prose matches.
/// </summary>
public static class DocumentTextFileTypes
{
    private static readonly Dictionary<string, DocumentTextFormat> Formats = new(StringComparer.Ordinal)
    {
        ["application/pdf"] = DocumentTextFormat.Pdf,
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] =
            DocumentTextFormat.WordprocessingMl,
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] =
            DocumentTextFormat.PresentationMl,
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = DocumentTextFormat.SpreadsheetMl,
        ["application/vnd.oasis.opendocument.text"] = DocumentTextFormat.OpenDocument,
        ["application/vnd.oasis.opendocument.spreadsheet"] = DocumentTextFormat.OpenDocumentSpreadsheet,
        ["application/vnd.oasis.opendocument.presentation"] = DocumentTextFormat.OpenDocument,
        ["application/epub+zip"] = DocumentTextFormat.Epub,
        ["text/plain"] = DocumentTextFormat.PlainText,
        ["text/markdown"] = DocumentTextFormat.PlainText
    };

    public static MimeMatcher Supported { get; } = new(types: Formats.Keys);

    /// <summary>
    /// The reader format for a MIME type, or null when its text cannot be read. The type is
    /// canonicalized first, so aliases resolve the same way they do everywhere else.
    /// </summary>
    public static DocumentTextFormat? FormatOf(string? mimeType)
    {
        var canonical = MimeAliases.Canonicalize(mimeType);

        return Formats.TryGetValue(canonical, out var format) ? format : null;
    }
}