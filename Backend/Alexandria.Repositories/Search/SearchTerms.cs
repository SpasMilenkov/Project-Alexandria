using System.Text.RegularExpressions;

namespace Alexandria.Repositories.Search;

/// <summary>
/// A search query split into the word tokens PostgreSQL's text-search parser would
/// produce. Words split on any non letter/digit character, so "don't" yields "don" and
/// "t" just like to_tsvector, and non-Latin scripts survive intact. File, directory and
/// streaming search all parse through here so a query matches the same way everywhere.
/// </summary>
internal sealed partial class SearchTerms
{
    // Shorter fragments prefix-match a large share of any lyric corpus, so they only
    // participate in lyrics search as whole words, and tiny queries skip lyrics entirely.
    internal const int MinLyricsQueryLength = 3;
    internal const int MinLyricsPrefixLength = 3;

    // Document bodies are long, so a word shorter than this prefix-matches almost every one of
    // them. Such words still narrow the name match; they are just left out of the body query.
    internal const int MinContentTokenLength = 3;

    private SearchTerms(string normalizedQuery, IReadOnlyList<string> tokens)
    {
        NormalizedQuery = normalizedQuery;
        Tokens = tokens;
        ContentTokens = tokens.Where(t => t.Length >= MinContentTokenLength).ToList();
    }

    public string NormalizedQuery { get; }

    public IReadOnlyList<string> Tokens { get; }

    /// <summary>The words searched for inside document text.</summary>
    public IReadOnlyList<string> ContentTokens { get; }

    public bool SearchesContent => ContentTokens.Count > 0;

    /// <summary>
    /// Every word must appear somewhere in the document, each as a prefix, so "invoice" also
    /// finds "invoices" and a half-typed last word still matches. Order and distance are free,
    /// unlike the lyrics phrase query, because a document mentions things far apart.
    /// </summary>
    public string ContentTsQuery => string.Join(" & ", ContentTokens.Select(t => $"{t}:*"));

    /// <summary>
    /// A Postgres regex for the start of any content word, used only to find where in a document
    /// to cut an excerpt. Tokens hold nothing but letters and digits, so none of them can carry
    /// regex syntax.
    /// </summary>
    public string ContentWordStartPattern => $@"\m({string.Join('|', ContentTokens)})";

    public bool IsEmpty => Tokens.Count == 0;

    public string PrefixTsQuery => string.Join(" & ", Tokens.Select(t => $"{t}:*"));

    // Stored normalized names collapse separators into single spaces, so trigram
    // similarity compares against the tokens in the same shape.
    public string TrigramText => string.Join(' ', Tokens);

    public bool SearchesLyrics => Tokens.Sum(t => t.Length) >= MinLyricsQueryLength;

    public bool PrefixesLastLyricsToken =>
        Tokens.Count > 0 && Tokens[^1].Length >= MinLyricsPrefixLength;

    public string LyricsTsQuery
    {
        get
        {
            if (IsEmpty)
                return string.Empty;

            var phrase = string.Join(" <-> ", Tokens);

            if (PrefixesLastLyricsToken)
                return $"{phrase}:*";

            return phrase;
        }
    }

    // Blank text means "no text filter", which differs from text that has no words in it.
    public static SearchTerms? ParseOptional(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        return Parse(query);
    }

    public static SearchTerms Parse(string query)
    {
        var normalized = query.Normalize().Trim().ToLowerInvariant();

        var tokens = Tokenize(normalized);

        return new SearchTerms(normalized, tokens);
    }

    internal static IReadOnlyList<string> Tokenize(string text) =>
        WordPattern().Matches(text.Normalize().ToLowerInvariant())
            .Select(m => m.Value)
            .ToList();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordPattern();
}