namespace Alexandria.Services.Storage.Content.Mime;

/// <summary>
/// Extension to canonical MIME type map. It covers what content sniffing handles poorly (text,
/// code, office containers) plus a safety net for common media.
/// </summary>
internal static class ExtensionMimeMap
{
    private const string TextPlain = "text/plain";
    private const string TextJavaScript = "text/javascript";
    private const string TextTypeScript = "text/typescript";

    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Pptx = "application/vnd.openxmlformats-officedocument.presentationml.presentation";

    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Text and documents
        ["md"] = "text/markdown",
        ["markdown"] = "text/markdown",
        ["json"] = "application/json",
        ["jsonc"] = "application/jsonc",
        ["json5"] = "application/json5",
        ["jsonl"] = "application/x-ndjson",
        ["ndjson"] = "application/x-ndjson",
        ["csv"] = "text/csv",
        ["tsv"] = "text/tab-separated-values",
        ["txt"] = TextPlain,
        ["log"] = "text/x-log",
        ["srt"] = "text/x-srt",
        ["vtt"] = "text/x-vtt",
        ["ass"] = "text/x-ass",
        ["ssa"] = "text/x-ass",
        ["m3u"] = "text/x-m3u",
        ["m3u8"] = "text/x-m3u8",
        ["cue"] = "text/x-cue",
        ["lrc"] = "text/x-lrc",
        ["nfo"] = "text/x-nfo",
        ["gitignore"] = "text/x-gitignore",
        ["gitattributes"] = "text/x-gitattributes",
        ["dockerignore"] = "text/x-dockerignore",
        ["editorconfig"] = "text/x-editorconfig",
        ["htaccess"] = "text/x-htaccess",
        ["tex"] = "text/x-tex",
        ["latex"] = "text/x-tex",
        ["ltx"] = "text/x-tex",
        ["ini"] = TextPlain,
        ["conf"] = TextPlain,
        ["yml"] = "application/yaml",
        ["yaml"] = "application/yaml",
        ["toml"] = "application/toml",
        ["html"] = "text/html",
        ["htm"] = "text/html",
        ["css"] = "text/css",
        ["xml"] = "application/xml",
        ["svg"] = "image/svg+xml",
        ["rtf"] = "application/rtf",
        ["pdf"] = "application/pdf",

        // Code, stored under distinct types so policy rules can target code separately
        ["cs"] = "text/x-csharp",
        ["vue"] = "text/x-vue",
        ["js"] = TextJavaScript,
        ["mjs"] = TextJavaScript,
        ["cjs"] = TextJavaScript,
        ["jsx"] = TextJavaScript,
        ["tsx"] = TextTypeScript,
        ["mts"] = TextTypeScript,
        ["cts"] = TextTypeScript,
        ["py"] = "text/x-python",
        ["sql"] = "application/sql",
        ["sh"] = "application/x-sh",
        ["java"] = "text/x-java-source",
        ["go"] = "text/x-go",
        ["rs"] = "text/x-rust",
        ["c"] = "text/x-c",
        ["h"] = "text/x-c",
        ["cpp"] = "text/x-c++",
        ["php"] = "application/x-php",
        ["swift"] = "text/x-swift",
        ["scala"] = "text/x-scala",
        ["kt"] = "text/x-kotlin",
        ["kts"] = "text/x-kotlin-script",
        ["dart"] = "application/dart",
        ["mk"] = "text/x-makefile",
        ["sln"] = "text/x-sln",
        ["slnx"] = "text/x-slnx",
        ["ps1"] = "text/x-powershell",
        ["psm1"] = "text/x-powershell",
        ["psd1"] = "text/x-powershell",
        ["bat"] = "text/x-batch",
        ["cmd"] = "text/x-batch",
        ["rb"] = "text/x-ruby",
        ["rake"] = "text/x-ruby",
        ["gemspec"] = "text/x-ruby",
        ["ru"] = "text/x-ruby",
        ["lua"] = "text/x-lua",
        ["r"] = "text/x-r",
        ["jl"] = "text/x-julia",
        ["fs"] = "text/x-fsharp",
        ["fsi"] = "text/x-fsharp",
        ["fsx"] = "text/x-fsharp",
        ["vb"] = "text/x-vb",
        ["vbs"] = "text/x-vb",
        ["scss"] = "text/x-scss",
        ["sass"] = "text/x-sass",
        ["less"] = "text/x-less",
        ["tf"] = "text/x-hcl",
        ["tfvars"] = "text/x-hcl",
        ["hcl"] = "text/x-hcl",
        ["nix"] = "text/x-nix",
        ["cmake"] = "text/x-cmake",
        ["dockerfile"] = "text/x-dockerfile",
        ["containerfile"] = "text/x-containerfile",
        ["bzl"] = "text/x-bazel",
        ["bazel"] = "text/x-bazel",
        ["gradle"] = "text/x-gradle",
        ["groovy"] = "text/x-groovy",
        ["gvy"] = "text/x-groovy",
        ["diff"] = "text/x-diff",
        ["patch"] = "text/x-diff",

        // XML-based project and markup files
        ["csproj"] = "application/xml",
        ["fsproj"] = "application/xml",
        ["vbproj"] = "application/xml",
        ["props"] = "application/xml",
        ["targets"] = "application/xml",
        ["nuspec"] = "application/xml",
        ["resx"] = "application/xml",
        ["xaml"] = "application/xml",
        ["xsd"] = "application/xml",
        ["xsl"] = "application/xml",
        ["xslt"] = "application/xml",

        // Office and containers
        ["docx"] = Docx,
        ["xlsx"] = Xlsx,
        ["pptx"] = Pptx,
        ["odt"] = "application/vnd.oasis.opendocument.text",
        ["ods"] = "application/vnd.oasis.opendocument.spreadsheet",
        ["odp"] = "application/vnd.oasis.opendocument.presentation",
        ["epub"] = "application/epub+zip",
        ["jar"] = "application/java-archive",
        ["apk"] = "application/vnd.android.package-archive",
        ["zip"] = "application/zip",
        ["gz"] = "application/gzip",
        ["7z"] = "application/x-7z-compressed",
        ["rar"] = "application/x-rar-compressed",
        ["tar"] = "application/x-tar",
        ["xz"] = "application/x-xz",

        // Images
        ["png"] = "image/png",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["gif"] = "image/gif",
        ["webp"] = "image/webp",
        ["bmp"] = "image/bmp",
        ["tiff"] = "image/tiff",
        ["avif"] = "image/avif",

        // Audio
        ["mp3"] = "audio/mpeg",
        ["flac"] = "audio/flac",
        ["wav"] = "audio/wav",
        ["ogg"] = "audio/ogg",
        ["opus"] = "audio/ogg",
        ["m4a"] = "audio/mp4",
        ["aac"] = "audio/aac",

        // Video
        ["mp4"] = "video/mp4",
        ["m4v"] = "video/x-m4v",
        ["mkv"] = "video/x-matroska",
        ["webm"] = "video/webm",
        ["mov"] = "video/quicktime",
        ["avi"] = "video/x-msvideo"
    };

    /// <summary>
    /// Guard G1: .ts is TypeScript or an MPEG transport stream, so it maps to text/typescript only
    /// when the header looks like text. Without a header (encrypted uploads) the extension is
    /// skipped.
    /// </summary>
    private const string GuardedTypeScriptExtension = "ts";

    /// <summary>
    /// Returns the canonical type for the extension of <paramref name="fileName"/>, or null when
    /// the extension is unknown, missing, or its guard fails.
    /// </summary>
    public static string? Lookup(string? fileName, byte[]? header)
    {
        var extension = GetExtension(fileName);
        if (extension.Length == 0) return null;

        if (string.Equals(extension, GuardedTypeScriptExtension, StringComparison.OrdinalIgnoreCase))
            return MimeTextHeuristic.LooksLikeText(header) ? TextTypeScript : null;

        return Map.GetValueOrDefault(extension);
    }

    internal static IEnumerable<string> AllTypes => Map.Values.Append(TextPlain);

    private static string GetExtension(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return string.Empty;
        var extension = Path.GetExtension(fileName);
        return extension.Length > 1 ? extension[1..] : string.Empty;
    }
}
