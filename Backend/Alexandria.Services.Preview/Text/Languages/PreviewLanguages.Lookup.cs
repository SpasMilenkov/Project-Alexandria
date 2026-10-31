using Alexandria.Services.Preview.Text.Languages;

namespace Alexandria.Services.Preview.Text;

public static partial class PreviewLanguages
{
    // Built lazily after the language fields in the other partial files are initialized.
    private static readonly Lazy<Dictionary<string, LanguageSpec>> ByNameMap = new(CreateByNameMap);

    private static Dictionary<string, LanguageSpec> CreateByNameMap() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["plaintext"] = Plain, ["text"] = Plain, ["txt"] = Plain, ["log"] = Log,
        ["json"] = Json, ["jsonc"] = Json, ["json5"] = Json5, ["jsonl"] = Json, ["ndjson"] = Json,
        ["csv"] = Csv, ["tsv"] = Csv,
        ["javascript"] = JavaScript, ["js"] = JavaScript, ["jsx"] = JavaScript, ["mjs"] = JavaScript, ["cjs"] = JavaScript,
        ["typescript"] = TypeScript, ["ts"] = TypeScript, ["tsx"] = TypeScript, ["mts"] = TypeScript, ["cts"] = TypeScript,
        ["csharp"] = CSharp, ["cs"] = CSharp, ["c#"] = CSharp, ["csx"] = CSharp,
        ["python"] = Python, ["py"] = Python, ["pyw"] = Python,
        ["java"] = Java,
        ["go"] = Go, ["golang"] = Go,
        ["rust"] = Rust, ["rs"] = Rust,
        ["c"] = C, ["h"] = C,
        ["cpp"] = Cpp, ["c++"] = Cpp, ["cc"] = Cpp, ["cxx"] = Cpp, ["hpp"] = Cpp, ["hh"] = Cpp, ["hxx"] = Cpp,
        ["php"] = Php, ["phtml"] = Php,
        ["swift"] = Swift,
        ["scala"] = Scala,
        ["kotlin"] = Kotlin, ["kt"] = Kotlin, ["kts"] = Kotlin,
        ["dart"] = Dart,
        ["sql"] = Sql,
        ["bash"] = Bash, ["sh"] = Bash, ["shell"] = Bash, ["zsh"] = Bash,
        ["powershell"] = PowerShell, ["ps1"] = PowerShell, ["psm1"] = PowerShell, ["psd1"] = PowerShell,
        ["batch"] = Batch, ["bat"] = Batch, ["cmd"] = Batch,
        ["ruby"] = Ruby, ["rb"] = Ruby, ["rake"] = Ruby, ["gemspec"] = Ruby, ["ru"] = Ruby,
        ["lua"] = Lua,
        ["r"] = R,
        ["julia"] = Julia, ["jl"] = Julia,
        ["fsharp"] = FSharp, ["fs"] = FSharp, ["fsi"] = FSharp, ["fsx"] = FSharp,
        ["vb"] = VisualBasic, ["vbnet"] = VisualBasic, ["vbs"] = VisualBasic,
        ["scss"] = Scss, ["sass"] = Sass, ["less"] = Less,
        ["hcl"] = Hcl, ["tf"] = Hcl, ["tfvars"] = Hcl, ["terraform"] = Hcl,
        ["nix"] = Nix,
        ["cmake"] = Cmake, ["cmakelists"] = Cmake,
        ["bazel"] = Bazel, ["bzl"] = Bazel, ["starlark"] = Bazel,
        ["groovy"] = Groovy, ["gradle"] = Groovy, ["gvy"] = Groovy,
        ["diff"] = Diff, ["patch"] = Diff,
        ["srt"] = Srt, ["vtt"] = Vtt, ["ass"] = Ass, ["ssa"] = Ass,
        ["m3u"] = M3u, ["m3u8"] = M3u, ["cue"] = Cue, ["lrc"] = Lrc, ["nfo"] = Nfo,
        ["ignore"] = Ignore, ["gitignore"] = Ignore, ["gitattributes"] = Ignore, ["dockerignore"] = Ignore,
        ["editorconfig"] = Ini, ["htaccess"] = Htaccess, ["nginx"] = Nginx,
        ["dockerfile"] = Dockerfile, ["containerfile"] = Dockerfile,
        ["makefile"] = Makefile, ["mk"] = Makefile, ["mak"] = Makefile,
        ["yaml"] = Yaml, ["yml"] = Yaml,
        ["toml"] = Toml,
        ["ini"] = Ini, ["cfg"] = Ini, ["conf"] = Ini, ["env"] = Ini, ["properties"] = Ini,
        ["tex"] = Tex, ["latex"] = Tex, ["ltx"] = Tex,
        ["css"] = Css,
        ["xml"] = Xml, ["html"] = Xml, ["htm"] = Xml, ["xhtml"] = Xml, ["svg"] = Xml, ["vue"] = Xml,
        ["csproj"] = Xml, ["fsproj"] = Xml, ["vbproj"] = Xml, ["props"] = Xml, ["targets"] = Xml,
        ["nuspec"] = Xml, ["resx"] = Xml, ["xaml"] = Xml, ["plist"] = Xml,
        ["xsd"] = Xml, ["xsl"] = Xml, ["xslt"] = Xml,
        ["sln"] = Sln, ["slnx"] = Xml,
        ["markdown"] = Markdown, ["md"] = Markdown
    };

    /// <summary>
    /// Bazel only recognises these exact spellings, and as plain words they are also common names
    /// for scripts and notes. They are matched case-sensitively and give way to a shebang.
    /// </summary>
    private static readonly HashSet<string> AmbiguousBazelFileNames = new(StringComparer.Ordinal)
    {
        "BUILD", "WORKSPACE"
    };

    private static readonly HashSet<string> RubyFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Gemfile", "Rakefile", "Vagrantfile", "Brewfile", "Podfile", "Fastfile", "Guardfile", "Capfile",
        "Thorfile", "Berksfile", "Dangerfile", "Steepfile"
    };

    /// <summary>Looks up a language by id, alias or file extension. Unknown names are plain text.</summary>
    public static LanguageSpec ByName(string? name)
        => !string.IsNullOrWhiteSpace(name) && ByNameMap.Value.TryGetValue(name.Trim(), out var spec) ? spec : Plain;

    /// <summary>
    /// Picks the language for a file. Well-known names win, then a known extension, then the
    /// Dockerfile/Makefile name prefixes, and finally a shebang line for extensionless scripts.
    /// </summary>
    public static LanguageSpec FromFileName(string? fileName, string? firstLine = null)
    {
        var name = BaseName(fileName);
        var hasShebang = firstLine is not null && firstLine.StartsWith("#!", StringComparison.Ordinal);

        if (name.Equals("CMakeLists.txt", StringComparison.OrdinalIgnoreCase)) return Cmake;
        if (name.Equals("nginx.conf", StringComparison.OrdinalIgnoreCase)) return Nginx;
        if (AmbiguousBazelFileNames.Contains(name) && !hasShebang) return Bazel;
        if (RubyFileNames.Contains(name)) return Ruby;
        if (name.Equals("Jenkinsfile", StringComparison.OrdinalIgnoreCase)) return Groovy;

        var extension = Path.GetExtension(name);
        if (extension.Length > 1 && ByNameMap.Value.TryGetValue(extension[1..], out var byExtension))
            return byExtension;

        if (HasNamePrefix(name, "Dockerfile") || HasNamePrefix(name, "Containerfile"))
            return Dockerfile;

        if (HasNamePrefix(name, "Makefile") ||
            name.Equals("GNUmakefile", StringComparison.OrdinalIgnoreCase))
            return Makefile;

        if (hasShebang)
            return FromShebang(firstLine!);

        return Plain;
    }

    private static LanguageSpec FromShebang(string firstLine)
    {
        if (firstLine.Contains("python", StringComparison.OrdinalIgnoreCase)) return Python;
        if (firstLine.Contains("node", StringComparison.OrdinalIgnoreCase)) return JavaScript;
        if (firstLine.Contains("ruby", StringComparison.OrdinalIgnoreCase)) return Ruby;
        if (firstLine.Contains("lua", StringComparison.OrdinalIgnoreCase)) return Lua;
        if (firstLine.Contains("pwsh", StringComparison.OrdinalIgnoreCase)) return PowerShell;
        if (firstLine.Contains("powershell", StringComparison.OrdinalIgnoreCase)) return PowerShell;
        if (firstLine.Contains("julia", StringComparison.OrdinalIgnoreCase)) return Julia;
        if (firstLine.Contains("Rscript", StringComparison.Ordinal)) return R;
        if (firstLine.Contains("sh", StringComparison.OrdinalIgnoreCase)) return Bash;

        return Plain;
    }

    /// <summary>
    /// The last path segment. Backslashes count as separators too, because names coming from
    /// archives built on Windows keep them, and Path.GetFileName ignores them on Linux.
    /// </summary>
    private static string BaseName(string? fileName)
        => string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetFileName(fileName.Replace('\\', '/'));

    private static bool HasNamePrefix(string name, string prefix)
        => name.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
           name.StartsWith($"{prefix}.", StringComparison.OrdinalIgnoreCase) ||
           name.StartsWith($"{prefix}-", StringComparison.OrdinalIgnoreCase);
}
