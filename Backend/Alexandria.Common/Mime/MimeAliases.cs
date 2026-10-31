namespace Alexandria.Common.Mime;

/// <summary>
/// The single place where MIME aliases are defined. The resolver, the file categorizer, policy
/// groups and the frontend catalog all canonicalize through this table, so an alias and its
/// canonical form always behave the same.
/// </summary>
public static class MimeAliases
{
    public const string Octet = "application/octet-stream";

    private static readonly Dictionary<string, string> AliasToCanonical = Build(
        ("image/jpeg", ["image/jpg", "image/pjpeg"]),
        ("image/bmp", ["image/x-ms-bmp", "image/x-bmp"]),
        ("audio/wav", ["audio/x-wav", "audio/wave", "audio/vnd.wave"]),
        ("audio/flac", ["audio/x-flac"]),
        ("audio/mpeg", ["audio/mp3", "audio/x-mp3", "audio/mpeg3"]),
        ("audio/mp4", ["audio/x-m4a"]),
        ("audio/ogg", ["audio/x-ogg"]),
        ("video/x-msvideo", ["video/avi", "video/msvideo"]),
        ("application/gzip", ["application/x-gzip", "application/x-gz"]),
        ("application/zip", ["application/x-zip", "application/x-zip-compressed"]),
        ("application/x-rar-compressed", ["application/vnd.rar", "application/x-rar"]),
        ("text/markdown", ["text/x-markdown"]),
        ("application/xml", ["text/xml"]),
        ("text/javascript", ["application/javascript", "application/x-javascript"]),
        ("text/typescript", ["application/typescript", "text/x-typescript"]),
        ("text/x-tex", ["application/x-tex", "application/x-latex", "text/x-latex"]),
        ("text/x-makefile", ["text/x-make", "application/x-makefile"]),
        ("text/x-log", ["application/x-log"]),
        ("application/x-php", ["text/x-php", "application/x-httpd-php", "application/php"]),
        ("application/dart", ["text/x-dart", "text/dart"]),
        ("text/x-kotlin", ["application/x-kotlin", "text/kotlin"]),
        ("text/x-scala", ["application/x-scala", "text/scala"]),
        ("text/x-swift", ["application/x-swift", "text/swift"]),
        ("text/x-powershell", ["application/x-powershell", "text/x-ps1"]),
        ("text/x-batch", ["application/x-bat", "application/x-batch", "text/x-bat", "text/x-msdos-batch"]),
        ("text/x-ruby", ["application/x-ruby", "text/ruby"]),
        ("text/x-lua", ["application/x-lua", "text/lua"]),
        ("text/x-r", ["application/x-r", "text/r"]),
        ("text/x-julia", ["application/x-julia", "text/julia"]),
        ("text/x-fsharp", ["application/x-fsharp", "text/fsharp"]),
        ("text/x-vb", ["application/x-vb", "text/x-vbnet", "application/x-vbnet", "text/x-visualbasic"]),
        ("text/x-scss", ["application/x-scss", "text/scss"]),
        ("text/x-sass", ["application/x-sass", "text/sass"]),
        ("text/x-less", ["application/x-less", "text/less"]),
        ("text/x-hcl", ["application/x-hcl", "text/x-terraform", "application/x-terraform"]),
        ("text/x-nix", ["application/x-nix", "text/nix"]),
        ("text/x-cmake", ["application/x-cmake", "text/cmake"]),
        ("text/x-bazel", ["application/x-bazel", "text/x-starlark", "application/x-starlark", "text/starlark"]),
        ("text/x-gradle", ["application/x-gradle", "text/gradle"]),
        ("text/x-groovy", ["application/x-groovy", "text/groovy"]),
        ("application/jsonc", ["text/x-jsonc", "application/x-jsonc"]),
        ("application/json5", ["text/x-json5", "application/x-json5"]),
        ("application/x-ndjson", ["application/jsonl", "application/x-jsonlines", "text/x-ndjson", "text/x-jsonl"]),
        ("text/x-diff", ["application/x-diff", "text/x-patch", "application/x-patch", "text/patch"]),
        ("text/x-srt", ["application/x-subrip", "application/x-srt", "text/srt"]),
        ("text/x-vtt", ["text/vtt", "application/x-vtt"]),
        ("text/x-ass", ["text/x-ssa", "application/x-ass", "application/x-ssa", "text/ssa"]),
        ("text/x-m3u", ["audio/x-mpegurl", "audio/mpegurl", "application/x-mpegurl", "application/mpegurl"]),
        ("text/x-m3u8", ["application/vnd.apple.mpegurl", "application/x-m3u8", "text/x-hls"]),
        ("text/x-cue", ["application/x-cue", "application/x-cuesheet", "text/x-cuesheet"]),
        ("text/x-lrc", ["application/x-lrc", "text/lrc"]),
        ("text/x-nfo", ["application/x-nfo", "text/nfo"]),
        ("text/x-gitignore", ["text/x-git-ignore", "application/x-gitignore"]),
        ("text/x-gitattributes", ["text/x-git-attributes", "application/x-gitattributes"]),
        ("text/x-dockerignore", ["text/x-docker-ignore", "application/x-dockerignore"]),
        ("text/x-editorconfig", ["text/x-editor-config", "application/x-editorconfig"]),
        ("text/x-htaccess", ["text/x-apache-htaccess", "application/x-htaccess"]),
        ("text/x-nginx", ["text/x-nginx-conf", "application/x-nginx", "text/nginx"]),
        ("application/yaml", ["application/x-yaml", "text/yaml", "text/x-yaml"]),
        ("application/toml", ["text/toml", "text/x-toml", "application/x-toml"]),
        ("text/x-c", ["text/x-csrc", "text/x-chdr"]),
        ("text/x-c++", ["text/x-c++src", "text/x-c++hdr"]),
        ("application/x-sh", ["application/x-shellscript", "text/x-sh", "text/x-shellscript"]),
        ("text/x-python", ["text/x-python3"]),
        (Octet, ["binary/octet-stream", "application/x-octet-stream"]));

    /// <summary>
    /// Normalizes a MIME string: trims, lowercases, strips parameters after a semicolon and maps
    /// known aliases to their canonical form. Returns an empty string for null or blank input.
    /// Idempotent.
    /// </summary>
    public static string Canonicalize(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType)) return string.Empty;

        var span = mimeType.AsSpan();
        var semicolon = span.IndexOf(';');
        if (semicolon >= 0) span = span[..semicolon];

        var normalized = span.Trim().ToString().ToLowerInvariant();
        return AliasToCanonical.GetValueOrDefault(normalized, normalized);
    }

    /// <summary>True when the string looks like <c>type/subtype</c> with no whitespace.</summary>
    public static bool HasValidShape(string canonical)
    {
        var slash = canonical.IndexOf('/');
        if (slash <= 0 || slash == canonical.Length - 1) return false;
        if (canonical.LastIndexOf('/') != slash) return false;

        foreach (var c in canonical)
            if (char.IsWhiteSpace(c))
                return false;

        return true;
    }

    /// <summary>
    /// True when a canonical type carries information: it has a valid shape and is not
    /// application/octet-stream.
    /// </summary>
    public static bool IsUsable(string canonical)
        => HasValidShape(canonical) && !string.Equals(canonical, Octet, StringComparison.Ordinal);

    /// <summary>Alias to canonical pairs, exposed for tests that assert the table is self-consistent.</summary>
    internal static IReadOnlyDictionary<string, string> Table => AliasToCanonical;

    private static Dictionary<string, string> Build(params (string Canonical, string[] Aliases)[] groups)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (canonical, aliases) in groups)
        foreach (var alias in aliases)
            map.Add(alias, canonical);
        return map;
    }
}
