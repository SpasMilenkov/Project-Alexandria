namespace Alexandria.Services.Preview.Text.Languages;

public enum HighlightMode
{
    Plain,
    Code,
    Markup,
    Markdown,
    Diff,
    Subtitle
}

/// <summary>
/// Describes how to tokenize one language. This is a lexical approximation (comments, strings,
/// numbers, keywords), not a parser.
/// </summary>
public sealed class LanguageSpec
{
    public required string Name { get; init; }
    public HighlightMode Mode { get; init; } = HighlightMode.Code;

    public HashSet<string> Keywords { get; init; } = [];
    public HashSet<string> Literals { get; init; } = [];

    public string[] LineComments { get; init; } = [];
    public (string Open, string Close)[] BlockComments { get; init; } = [];

    /// <summary>Single-line string delimiters with backslash escapes.</summary>
    public string Quotes { get; init; } = "\"'";

    /// <summary>Single-line string delimiters without escapes (shell single quotes).</summary>
    public string RawQuotes { get; init; } = "";

    /// <summary>Delimiters of strings that may span lines (template literals, Go raw strings).</summary>
    public string MultilineQuotes { get; init; } = "";

    /// <summary>Field separators that get their own token outside strings, for CSV and similar data.</summary>
    public string Delimiters { get; init; } = "";

    public bool BackslashEscapes { get; init; } = true;
    public bool DoubledQuotes { get; init; }
    public bool TripleQuotes { get; init; }
    public bool CSharpStrings { get; init; }
    public bool StringPrefixes { get; init; }
    public bool ApostropheNeedsBoundary { get; init; }
    public bool HashNeedsBoundary { get; init; }

    /// <summary>Characters that make a preceding word or string a key, for example ":" or "=".</summary>
    public string KeyTerminators { get; init; } = "";

    public bool KeysInBraces { get; init; }
    public bool StrictKeyColon { get; init; }
    public bool HyphenIdents { get; init; }

    public bool HashDirectives { get; init; }
    public bool HashColors { get; init; }
    public bool AtKeywords { get; init; }
    public bool Sections { get; init; }
    public bool Variables { get; init; }
    public bool ParenthesizedVariables { get; init; }
    public bool PercentVariables { get; init; }
    public bool TypeHeuristic { get; init; }
    public bool FnHeuristic { get; init; } = true;
}
