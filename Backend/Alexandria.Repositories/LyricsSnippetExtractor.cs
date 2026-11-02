using System.Text.RegularExpressions;

namespace Alexandria.Repositories;

/// <summary>
/// Picks the lyric line that explains why a track matched a lyrics search. Mirrors the
/// phrase semantics of the lyrics tsquery: tokens must appear consecutively, the last one
/// may be a prefix. Phrases that wrap onto the next line are returned as both lines.
/// </summary>
internal static partial class LyricsSnippetExtractor
{
    internal const int MaxSnippetLength = 140;
    internal const string LineJoiner = " / ";

    public static string? Extract(string? plainLyrics, string? syncedLyrics, StreamingSearchTerms terms)
    {
        if (terms.IsEmpty)
            return null;

        var lines = ReadLines(plainLyrics, syncedLyrics);

        if (lines.Count == 0)
            return null;

        var words = lines.Select(StreamingSearchTerms.Tokenize).ToList();

        for (var i = 0; i < lines.Count; i++)
        {
            if (ContainsPhrase(words[i], terms))
                return Truncate(lines[i]);
        }

        for (var i = 0; i < lines.Count - 1; i++)
        {
            if (ContainsPhrase([.. words[i], .. words[i + 1]], terms))
                return Truncate($"{lines[i]}{LineJoiner}{lines[i + 1]}");
        }

        for (var i = 0; i < lines.Count; i++)
        {
            if (words[i].Any(word => MatchesAnyToken(word, terms)))
                return Truncate(lines[i]);
        }

        return null;
    }

    private static List<string> ReadLines(string? plainLyrics, string? syncedLyrics)
    {
        var source = plainLyrics;

        if (string.IsNullOrWhiteSpace(source))
            source = LrcTagPattern().Replace(syncedLyrics ?? string.Empty, " ");

        return source
            .Split('\n')
            .Select(line => WhitespacePattern().Replace(line, " ").Trim())
            .Where(line => line.Length > 0)
            .ToList();
    }

    private static bool ContainsPhrase(IReadOnlyList<string> words, StreamingSearchTerms terms)
    {
        var tokenCount = terms.Tokens.Count;

        for (var start = 0; start + tokenCount <= words.Count; start++)
        {
            var matched = true;

            for (var offset = 0; offset < tokenCount && matched; offset++)
                matched = MatchesToken(words[start + offset], terms, offset);

            if (matched)
                return true;
        }

        return false;
    }

    private static bool MatchesToken(string word, StreamingSearchTerms terms, int tokenIndex)
    {
        var token = terms.Tokens[tokenIndex];
        var isPrefix = tokenIndex == terms.Tokens.Count - 1 && terms.PrefixesLastLyricsToken;

        if (isPrefix)
            return word.StartsWith(token, StringComparison.Ordinal);

        return word == token;
    }

    private static bool MatchesAnyToken(string word, StreamingSearchTerms terms) =>
        Enumerable.Range(0, terms.Tokens.Count).Any(index => MatchesToken(word, terms, index));

    private static string Truncate(string snippet)
    {
        if (snippet.Length <= MaxSnippetLength)
            return snippet;

        return $"{snippet[..(MaxSnippetLength - 1)].TrimEnd()}…";
    }

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex LrcTagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
