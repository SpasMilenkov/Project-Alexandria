using System.Text.RegularExpressions;

namespace Alexandria.Repositories;

/// <summary>
/// A streaming search query split into the word tokens PostgreSQL's text-search parser
/// would produce. Words split on any non letter/digit character, so "don't" yields "don"
/// and "t" just like to_tsvector, and non-Latin scripts survive intact.
/// </summary>
internal sealed partial class StreamingSearchTerms
{
    // Shorter fragments prefix-match a large share of any lyric corpus, so they only
    // participate in lyrics search as whole words, and tiny queries skip lyrics entirely.
    internal const int MinLyricsQueryLength = 3;
    internal const int MinLyricsPrefixLength = 3;

    private StreamingSearchTerms(string normalizedQuery, IReadOnlyList<string> tokens)
    {
        NormalizedQuery = normalizedQuery;
        Tokens = tokens;
    }

    public string NormalizedQuery { get; }

    public IReadOnlyList<string> Tokens { get; }

    public bool IsEmpty => Tokens.Count == 0;

    public string MetadataTsQuery => string.Join(" & ", Tokens.Select(t => $"{t}:*"));

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

    public static StreamingSearchTerms Parse(string query)
    {
        var normalized = query.Normalize().Trim().ToLowerInvariant();

        var tokens = Tokenize(normalized);

        return new StreamingSearchTerms(normalized, tokens);
    }

    internal static IReadOnlyList<string> Tokenize(string text) =>
        WordPattern().Matches(text.Normalize().ToLowerInvariant())
            .Select(m => m.Value)
            .ToList();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordPattern();
}
