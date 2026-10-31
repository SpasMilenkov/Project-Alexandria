using Alexandria.Services.Preview.Text.Languages;

namespace Alexandria.Services.Preview.Text;

public static partial class PreviewLanguages
{
    // A property keeps partial-file initialization order from mattering for specs in other files.
    private static (string Open, string Close)[] CBlock => [("/*", "*/")];

    private static HashSet<string> Words(string words, bool ignoreCase = false)
        => new(words.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    public static readonly LanguageSpec Plain = new() { Name = "plaintext", Mode = HighlightMode.Plain };

    public static readonly LanguageSpec Log = new()
    {
        Name = "log",
        Keywords = Words("TRACE DEBUG INFO NOTICE WARN WARNING ERROR FATAL CRITICAL", ignoreCase: true),
        Quotes = "\"",
        FnHeuristic = false
    };

    public static readonly LanguageSpec Csv = new()
    {
        Name = "csv",
        Quotes = "\"",
        Delimiters = ",\t;",
        BackslashEscapes = false,
        DoubledQuotes = true,
        FnHeuristic = false
    };

    public static readonly LanguageSpec Json = new()
    {
        Name = "json",
        Quotes = "\"",
        Literals = Words("true false null"),
        LineComments = ["//"],
        BlockComments = CBlock,
        KeyTerminators = ":",
        FnHeuristic = false
    };

    public static readonly LanguageSpec Json5 = new()
    {
        Name = "json5",
        Quotes = "\"'",
        Literals = Words("true false null Infinity NaN"),
        LineComments = ["//"],
        BlockComments = CBlock,
        KeyTerminators = ":",
        FnHeuristic = false
    };
}
