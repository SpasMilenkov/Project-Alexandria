using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Mime;

/// <summary>
/// Maps canonical MIME types to a <see cref="FileCategory"/>, which decides how a file is previewed.
/// The rules are disjoint, so their order never changes the outcome.
/// </summary>
public static class MimeCategories
{
    private static readonly (FileCategory Category, MimeMatcher Matcher)[] Rules =
    [
        (FileCategory.Image, new MimeMatcher(prefixes: ["image/"])),
        (FileCategory.Audio, new MimeMatcher(prefixes: ["audio/"])),
        (FileCategory.Video, new MimeMatcher(prefixes: ["video/"])),
        (FileCategory.Text, new MimeMatcher(
            prefixes: ["text/"],
            types:
            [
                "application/json", "application/jsonc", "application/json5", "application/x-ndjson",
                "application/xml", "application/yaml", "application/toml", "application/sql",
                "application/x-sh", "application/x-php", "application/dart"
            ])),

        // EPUB is deliberately absent: the document worker has no EPUB renderer, so routing it there
        // would only queue a job that is bound to fail and be retried.
        (FileCategory.Document, new MimeMatcher(types:
        [
            "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.oasis.opendocument.text", "application/rtf"
        ])),
        (FileCategory.Spreadsheet, new MimeMatcher(types:
        [
            "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.oasis.opendocument.spreadsheet"
        ])),
        (FileCategory.Presentation, new MimeMatcher(types:
        [
            "application/vnd.ms-powerpoint",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "application/vnd.oasis.opendocument.presentation"
        ])),
        (FileCategory.Pdf, new MimeMatcher(types: ["application/pdf"])),
        (FileCategory.Archive, new MimeMatcher(types:
        [
            "application/zip", "application/x-7z-compressed", "application/x-rar-compressed",
            "application/gzip", "application/x-tar", "application/x-xz", "application/java-archive",
            "application/vnd.android.package-archive"
        ]))
    ];

    /// <summary>
    /// Every type a preview worker renders. Requesting a thumbnail for one of these queues a job,
    /// for anything else it never can, so the frontend reads this set from the generated catalog.
    /// </summary>
    public static MimeMatcher PreviewWorker { get; } =
        MimeMatcher.Union(Rules.Where(r => UsesPreviewWorker(r.Category)).Select(r => r.Matcher));

    internal static IReadOnlyList<(FileCategory Category, MimeMatcher Matcher)> AllRules => Rules;

    /// <summary>Category of a MIME type. Input is canonicalized first; unknown or blank input is Unknown.</summary>
    public static FileCategory Categorize(string? mimeType)
    {
        var canonical = MimeAliases.Canonicalize(mimeType);
        if (canonical.Length == 0) return FileCategory.Unknown;

        foreach (var (category, matcher) in Rules)
            if (matcher.Matches(canonical))
                return category;

        return FileCategory.Unknown;
    }

    public static bool IsText(string? mimeType) => Categorize(mimeType) == FileCategory.Text;

    /// <summary>Categories rendered asynchronously by a preview worker, which also produces the thumbnail.</summary>
    public static bool UsesPreviewWorker(FileCategory category) => category
        is FileCategory.Image or FileCategory.Audio or FileCategory.Video
        or FileCategory.Document or FileCategory.Spreadsheet or FileCategory.Presentation or FileCategory.Pdf;
}
