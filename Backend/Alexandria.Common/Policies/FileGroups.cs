using Alexandria.Common.Mime;

namespace Alexandria.Common.Policies;

/// <summary>
/// The file groups a policy rule can target, used as semicolon-separated TriggerValue entries for
/// FileGroup rules. The frontend reads these groups from the generated MIME catalog, so this class
/// is the only place they are defined. Entries are canonical types; input is canonicalized before
/// matching, so aliases never need to be listed.
/// </summary>
public static class FileGroups
{
    public const string Images = "Images";
    public const string Videos = "Videos";
    public const string Audio = "Audio";
    public const string Documents = "Documents";
    public const string Spreadsheets = "Spreadsheets";
    public const string Presentations = "Presentations";
    public const string Archives = "Archives";
    public const string Code = "Code";
    public const string Text = "Text";

    private static readonly (string Name, MimeMatcher Matcher)[] Groups =
    [
        (Images, new MimeMatcher(prefixes: ["image/"])),
        (Videos, new MimeMatcher(prefixes: ["video/"])),
        (Audio, new MimeMatcher(prefixes: ["audio/"])),
        (Documents, new MimeMatcher(types:
        [
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.template",
            "application/vnd.ms-word.document.macroenabled.12",
            "application/vnd.oasis.opendocument.text",
            "application/rtf",
            "application/epub+zip"
        ])),
        (Spreadsheets, new MimeMatcher(types:
        [
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.template",
            "application/vnd.ms-excel.sheet.macroenabled.12",
            "application/vnd.ms-excel.sheet.binary.macroenabled.12",
            "application/vnd.oasis.opendocument.spreadsheet",
            "text/csv",
            "text/tab-separated-values"
        ])),
        (Presentations, new MimeMatcher(types:
        [
            "application/vnd.ms-powerpoint",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "application/vnd.openxmlformats-officedocument.presentationml.slideshow",
            "application/vnd.openxmlformats-officedocument.presentationml.template",
            "application/vnd.ms-powerpoint.presentation.macroenabled.12",
            "application/vnd.oasis.opendocument.presentation"
        ])),
        (Archives, new MimeMatcher(types:
        [
            "application/zip",
            "application/x-rar-compressed",
            "application/x-7z-compressed",
            "application/x-tar",
            "application/gzip",
            "application/x-xz",
            "application/x-bzip2",
            "application/java-archive",
            "application/vnd.android.package-archive"
        ])),
        (Code, new MimeMatcher(types:
        [
            "text/javascript", "text/typescript", "text/html", "text/css",
            "application/json", "application/jsonc", "application/json5", "application/x-ndjson",
            "application/xml", "application/yaml", "application/toml",
            "text/x-python", "text/x-java-source", "text/x-c", "text/x-c++", "text/x-csharp",
            "text/x-go", "text/x-rust", "text/x-vue", "text/x-swift", "text/x-scala",
            "text/x-kotlin", "text/x-kotlin-script", "application/x-php", "application/dart",
            "application/sql", "application/x-sh", "text/x-powershell", "text/x-batch",
            "text/x-ruby", "text/x-lua", "text/x-r", "text/x-julia", "text/x-fsharp", "text/x-vb",
            "text/x-scss", "text/x-sass", "text/x-less", "text/x-hcl", "text/x-nix",
            "text/x-dockerfile", "text/x-containerfile", "text/x-makefile", "text/x-cmake",
            "text/x-bazel", "text/x-gradle", "text/x-groovy", "text/x-tex",
            "text/x-sln", "text/x-slnx", "text/x-diff", "text/x-htaccess", "text/x-nginx"
        ])),
        (Text, new MimeMatcher(types:
        [
            "text/plain", "text/markdown", "text/x-log", "text/x-srt", "text/x-vtt", "text/x-ass",
            "text/x-m3u", "text/x-m3u8", "text/x-cue", "text/x-lrc", "text/x-nfo",
            "text/x-gitignore", "text/x-gitattributes", "text/x-dockerignore", "text/x-editorconfig"
        ]))
    ];

    private static readonly Dictionary<string, MimeMatcher> ByName =
        Groups.ToDictionary(g => g.Name, g => g.Matcher, StringComparer.Ordinal);

    /// <summary>Every group in display order, exported to the frontend catalog.</summary>
    internal static IReadOnlyList<(string Name, MimeMatcher Matcher)> All => Groups;

    /// <summary>
    /// True when the type belongs to any of the semicolon-separated groups. The type is
    /// canonicalized first, so case, MIME parameters and aliases do not matter.
    /// </summary>
    public static bool MatchesMimeType(string groupsCsv, string mimeType)
    {
        var canonical = MimeAliases.Canonicalize(mimeType);
        if (canonical.Length == 0) return false;

        var groups = groupsCsv.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var group in groups)
        {
            if (!ByName.TryGetValue(group, out var matcher)) continue;
            if (matcher.Matches(canonical)) return true;
        }

        return false;
    }
}
