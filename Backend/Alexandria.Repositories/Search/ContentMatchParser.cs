using System.Text;
using Alexandria.Dto.Files;

namespace Alexandria.Repositories.Search;

/// <summary>
/// Turns <c>ts_headline</c> output into plain segments. The query asks for control characters as
/// match and excerpt markers: the text sweep strips those from every document, so they can never
/// come from the text itself, and the client gets no markup to render.
/// </summary>
internal static class ContentMatchParser
{
    internal const char FragmentDelimiter = '\u0001';
    internal const char MatchStart = '\u0002';
    internal const char MatchEnd = '\u0003';

    /// <summary>The <c>ts_headline</c> options that produce what <see cref="Parse"/> reads.</summary>
    internal static string HeadlineOptions(int maxFragments) =>
        $"MaxFragments={maxFragments}, MinWords=6, MaxWords=24, "
        + $"FragmentDelimiter=\"{FragmentDelimiter}\", StartSel=\"{MatchStart}\", StopSel=\"{MatchEnd}\"";

    /// <summary>
    /// Excerpts that contain at least one match. Page breaks and line breaks become spaces, since
    /// an excerpt reads as one line.
    /// </summary>
    public static List<ContentMatchFragment> Parse(string headline)
    {
        var fragments = new List<ContentMatchFragment>();

        foreach (var raw in headline.Split(FragmentDelimiter))
        {
            var segments = ParseFragment(raw);

            if (segments.Any(s => s.IsMatch))
                fragments.Add(new ContentMatchFragment(segments));
        }

        return fragments;
    }

    private static List<ContentMatchSegment> ParseFragment(string raw)
    {
        var segments = new List<ContentMatchSegment>();
        var current = new StringBuilder();
        var inMatch = false;

        foreach (var c in raw)
        {
            if (c is MatchStart or MatchEnd)
            {
                Flush(segments, current, inMatch);
                inMatch = c == MatchStart;
                continue;
            }

            current.Append(c is '\f' or '\n' or '\r' ? ' ' : c);
        }

        Flush(segments, current, inMatch);
        TrimEdges(segments);

        return segments;
    }

    private static void Flush(List<ContentMatchSegment> segments, StringBuilder current, bool isMatch)
    {
        if (current.Length == 0)
            return;

        segments.Add(new ContentMatchSegment(current.ToString(), isMatch));
        current.Clear();
    }

    // An excerpt starts and ends mid-text, so only the outer edges lose their whitespace.
    private static void TrimEdges(List<ContentMatchSegment> segments)
    {
        if (segments.Count == 0)
            return;

        segments[0] = segments[0] with { Text = segments[0].Text.TrimStart() };
        segments[^1] = segments[^1] with { Text = segments[^1].Text.TrimEnd() };
        segments.RemoveAll(s => s.Text.Length == 0);
    }
}