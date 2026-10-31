namespace Alexandria.Services.Storage.Content.Mime;

/// <summary>
/// Filename rules for well-known text files that have no reliable extension. Content sniffers can
/// mistake these files for other text formats, so the filename is the stronger signal.
/// </summary>
internal static class MimeFileNameMap
{
    public const string Dockerfile = "text/x-dockerfile";
    public const string Containerfile = "text/x-containerfile";
    public const string Makefile = "text/x-makefile";
    public const string Cmake = "text/x-cmake";
    public const string Bazel = "text/x-bazel";
    public const string Ruby = "text/x-ruby";
    public const string Groovy = "text/x-groovy";
    public const string Nginx = "text/x-nginx";

    private static readonly HashSet<string> ExtensionOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        "CMakeLists.txt",
        "nginx.conf"
    };

    private static readonly Dictionary<string, string> ExactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CMakeLists.txt"] = Cmake,
        ["nginx.conf"] = Nginx,
        ["BUILD.bazel"] = Bazel,
        ["WORKSPACE.bazel"] = Bazel,
        ["MODULE.bazel"] = Bazel,
        ["Gemfile"] = Ruby,
        ["Rakefile"] = Ruby,
        ["Vagrantfile"] = Ruby,
        ["Brewfile"] = Ruby,
        ["Podfile"] = Ruby,
        ["Fastfile"] = Ruby,
        ["Guardfile"] = Ruby,
        ["Capfile"] = Ruby,
        ["Thorfile"] = Ruby,
        ["Berksfile"] = Ruby,
        ["Dangerfile"] = Ruby,
        ["Steepfile"] = Ruby,
        ["Jenkinsfile"] = Groovy
    };

    /// <summary>
    /// Bazel only recognises these exact spellings, and as plain words they are also common names
    /// for scripts and notes. They are matched case-sensitively and give way to a shebang.
    /// </summary>
    private static readonly Dictionary<string, string> AmbiguousNames = new(StringComparer.Ordinal)
    {
        ["BUILD"] = Bazel,
        ["WORKSPACE"] = Bazel
    };

    /// <summary>
    /// Returns the type implied by the file name, or null when the name is not well known.
    /// When <paramref name="header"/> is given and starts with a shebang, ambiguous names are
    /// ignored so the file is resolved as the script it is.
    /// </summary>
    public static string? Lookup(string? fileName, byte[]? header = null)
    {
        var name = BaseName(fileName);
        if (name.Length == 0) return null;

        if (ExactNames.TryGetValue(name, out var exact)) return exact;

        if (AmbiguousNames.TryGetValue(name, out var ambiguous))
            return StartsWithShebang(header) ? null : ambiguous;

        if (MatchesPrefix(name, "Dockerfile")) return Dockerfile;
        if (MatchesPrefix(name, "Containerfile")) return Containerfile;

        if (MatchesPrefix(name, "Makefile") ||
            name.Equals("GNUmakefile", StringComparison.OrdinalIgnoreCase))
            return Makefile;

        return null;
    }

    /// <summary>
    /// True for a known name whose type is more specific than its extension, such as
    /// CMakeLists.txt or nginx.conf, which would otherwise resolve as plain text.
    /// </summary>
    public static bool OverridesExtension(string? fileName)
        => ExtensionOverrides.Contains(BaseName(fileName));

    internal static IEnumerable<string> AllTypes =>
        [Dockerfile, Containerfile, Makefile, Cmake, Bazel, Ruby, Groovy, Nginx];

    /// <summary>
    /// The last path segment. Backslashes count as separators too, because names coming from
    /// archives built on Windows keep them, and Path.GetFileName ignores them on Linux.
    /// </summary>
    private static string BaseName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;

        return Path.GetFileName(fileName.Replace('\\', '/'));
    }

    private static bool StartsWithShebang(byte[]? header)
        => header is { Length: >= 2 } && header[0] == (byte)'#' && header[1] == (byte)'!';

    private static bool MatchesPrefix(string name, string prefix)
        => name.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
           name.StartsWith($"{prefix}.", StringComparison.OrdinalIgnoreCase) ||
           name.StartsWith($"{prefix}-", StringComparison.OrdinalIgnoreCase);
}
