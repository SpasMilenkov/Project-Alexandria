using Alexandria.Services.Preview.Text.Languages;

namespace Alexandria.Services.Preview.Text;

public static partial class PreviewLanguages
{
    public static readonly LanguageSpec Dockerfile = new()
    {
        Name = "dockerfile",
        Keywords = Words(
            "FROM RUN CMD LABEL EXPOSE ENV ADD COPY ENTRYPOINT VOLUME USER WORKDIR ARG ONBUILD STOPSIGNAL " +
            "HEALTHCHECK SHELL AS MAINTAINER", ignoreCase: true),
        LineComments = ["#"],
        HashNeedsBoundary = true,
        Variables = true
    };

    public static readonly LanguageSpec Makefile = new()
    {
        Name = "makefile",
        Keywords = Words(
            "ifeq ifneq ifdef ifndef else endif include define endef undefine export unexport override vpath"),
        LineComments = ["#"],
        Variables = true,
        ParenthesizedVariables = true,
        ApostropheNeedsBoundary = true,
        FnHeuristic = false
    };

    public static readonly LanguageSpec Yaml = new()
    {
        Name = "yaml",
        Literals = Words("true false null yes no on off"),
        LineComments = ["#"],
        HashNeedsBoundary = true,
        DoubledQuotes = true,
        ApostropheNeedsBoundary = true,
        KeyTerminators = ":",
        StrictKeyColon = true,
        HyphenIdents = true,
        FnHeuristic = false
    };

    public static readonly LanguageSpec Toml = new()
    {
        Name = "toml",
        Literals = Words("true false"),
        LineComments = ["#"],
        HashNeedsBoundary = true,
        TripleQuotes = true,
        KeyTerminators = "=",
        HyphenIdents = true,
        Sections = true,
        FnHeuristic = false
    };

    public static readonly LanguageSpec Ini = new()
    {
        Name = "ini",
        Literals = Words("true false"),
        LineComments = ["#", ";"],
        HashNeedsBoundary = true,
        ApostropheNeedsBoundary = true,
        KeyTerminators = "=",
        HyphenIdents = true,
        Sections = true,
        FnHeuristic = false
    };

    public static readonly LanguageSpec Hcl = new()
    {
        Name = "hcl",
        Keywords = Words(
            "resource data variable output module provider terraform locals for_each count depends_on lifecycle " +
            "dynamic content true false null"),
        Literals = Words("true false null"),
        LineComments = ["#", "//"],
        BlockComments = CBlock,
        Quotes = "\"",
        KeyTerminators = "=",
        HyphenIdents = true,
        Variables = true,
        FnHeuristic = true
    };

    public static readonly LanguageSpec Nix = new()
    {
        Name = "nix",
        Keywords = Words(
            "let in with rec inherit if then else assert or and not import builtins derivation true false null"),
        Literals = Words("true false null"),
        LineComments = ["#"],
        BlockComments = CBlock,
        Quotes = "\"",
        Variables = true,
        FnHeuristic = true
    };

    public static readonly LanguageSpec Cmake = new()
    {
        Name = "cmake",
        Keywords = Words(
            "if elseif else endif foreach endforeach while endwhile function endfunction macro endmacro set unset list " +
            "message project cmake_minimum_required include add_executable add_library target_link_libraries " +
            "target_include_directories target_compile_options install find_package option file string math return " +
            "break continue", ignoreCase: true),
        Literals = Words("true false on off yes no", ignoreCase: true),
        LineComments = ["#"],
        BlockComments = [("#[[", "]]")],
        Quotes = "\"",
        HashNeedsBoundary = true,
        Variables = true,
        FnHeuristic = true
    };

    public static readonly LanguageSpec Bazel = new()
    {
        Name = "bazel",
        Keywords = Words(
            "def return if elif else for in load package rule attr select glob deps srcs visibility licenses " +
            "exports_files filegroup genrule alias workspace repository http_archive git_repository local_repository " +
            "register_toolchains"),
        Literals = Words("True False None"),
        LineComments = ["#"],
        Quotes = "\"'",
        TripleQuotes = true,
        TypeHeuristic = true
    };

    public static readonly LanguageSpec Groovy = new()
    {
        Name = "groovy",
        Keywords = Words(
            "abstract assert break case catch class const continue def default do else enum extends final finally for " +
            "goto if implements import in instanceof interface native new package private protected public return " +
            "static strictfp super switch synchronized this throw throws transient try volatile while as trait plugins " +
            "dependencies repositories tasks apply implementation api compileOnly runtimeOnly testImplementation " +
            "testRuntimeOnly"),
        Literals = Words("true false null"),
        LineComments = ["//"],
        BlockComments = CBlock,
        Quotes = "\"'",
        TripleQuotes = true,
        TypeHeuristic = true
    };

    public static readonly LanguageSpec Css = new()
    {
        Name = "css",
        BlockComments = CBlock,
        KeyTerminators = ":",
        KeysInBraces = true,
        HyphenIdents = true,
        HashColors = true,
        AtKeywords = true
    };

    public static readonly LanguageSpec Scss = new()
    {
        Name = "scss",
        LineComments = ["//"],
        BlockComments = CBlock,
        KeyTerminators = ":",
        KeysInBraces = true,
        HyphenIdents = true,
        HashColors = true,
        AtKeywords = true,
        Variables = true
    };

    public static readonly LanguageSpec Sass = new()
    {
        Name = "sass",
        LineComments = ["//"],
        BlockComments = CBlock,
        KeyTerminators = ":",
        HyphenIdents = true,
        HashColors = true,
        AtKeywords = true,
        Variables = true
    };

    public static readonly LanguageSpec Less = new()
    {
        Name = "less",
        LineComments = ["//"],
        BlockComments = CBlock,
        KeyTerminators = ":",
        KeysInBraces = true,
        HyphenIdents = true,
        HashColors = true,
        AtKeywords = true
    };

    public static readonly LanguageSpec Tex = new()
    {
        Name = "tex",
        Keywords = Words(
            "begin end documentclass usepackage section subsection subsubsection paragraph label ref cite item itemize " +
            "enumerate figure table input include bibliography bibliographystyle maketitle newcommand renewcommand " +
            "def let infty partial sum int frac sqrt left right textbf textit emph"),
        LineComments = ["%"],
        Quotes = "",
        FnHeuristic = false
    };

    public static readonly LanguageSpec Sln = new()
    {
        Name = "sln",
        Keywords = Words(
            "Microsoft Visual Studio Solution File Format Version Project EndProject Global GlobalSection " +
            "EndGlobalSection NestedProjects SolutionConfigurationPlatforms ProjectConfigurationPlatforms " +
            "SolutionProperties ExtensibilityGlobals",
            ignoreCase: true),
        LineComments = ["#"],
        Quotes = "\"",
        KeyTerminators = "=",
        FnHeuristic = false
    };
}
