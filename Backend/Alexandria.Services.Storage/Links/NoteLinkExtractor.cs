using System.Text;
using System.Text.RegularExpressions;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Links;

namespace Alexandria.Services.Storage.Links;

/// <summary>
/// Reads the <c>alexandria://</c> links out of a note's markdown, by the same rules as the editor's
/// Links list: every form counts (autolinks, labelled links, embeds), links inside code spans and
/// fenced blocks are examples rather than links, and each target is listed once.
/// </summary>
internal static partial class NoteLinkExtractor
{
    /// <summary>Raise when the rules change, so the sweep reads every note again.</summary>
    public const int CurrentVersion = 1;

    // Far more than anyone links from one note; it only bounds what a hostile upload can store.
    internal const int MaxTargets = 1000;

    internal const int SnippetLength = 240;

    // Room before the link when a long line is cut, so the snippet reads as a sentence about it.
    private const int SnippetLead = 100;

    // Labels can be long; a snippet over this is cut regardless, to fit the column.
    private const int SnippetHardLimit = 900;

    private const string Ellipsis = "…";

    private static readonly Dictionary<string, LinkTargetKind> KindsBySegment = new()
    {
        ["file"] = LinkTargetKind.File,
        ["folder"] = LinkTargetKind.Folder,
        ["tag"] = LinkTargetKind.Tag,
        ["playlist"] = LinkTargetKind.Playlist,
    };

    public static IReadOnlyList<ExtractedNoteLink> Extract(string markdown)
    {
        var text = BlankCodeSpans(BlankFencedCode(markdown));
        var seen = new HashSet<(LinkTargetKind, Guid)>();
        var links = new List<ExtractedNoteLink>();

        // Links come in text order, so a line is read once however many links share it. Reading it
        // per link made a 1 MB line holding the full 1000 links cost seconds.
        var line = (Body: string.Empty, BodyStart: 0, End: -1, Tokens: new List<(int Start, int End)>());

        foreach (Match match in AnyLink().Matches(text))
        {
            var kind = KindsBySegment[match.Groups["segment"].Value];
            var id = Guid.Parse(match.Groups["id"].Value);

            if (!seen.Add((kind, id))) continue;

            if (match.Index >= line.End) line = ReadLine(markdown, match.Index);

            var linkAt = Math.Clamp(match.Index - line.BodyStart, 0, line.Body.Length);

            links.Add(new ExtractedNoteLink(kind, id, Storable(Window(line.Body, linkAt, line.Tokens))));

            if (links.Count == MaxTargets) break;
        }

        return links;
    }

    // Code is replaced by spaces of the same length, so positions in the result still point at the
    // same characters in the original text.
    private static string BlankFencedCode(string markdown)
    {
        var builder = new StringBuilder(markdown.Length);
        string? fence = null;
        var position = 0;

        while (position < markdown.Length)
        {
            var end = markdown.IndexOf('\n', position);
            var next = end < 0 ? markdown.Length : end + 1;
            var line = markdown[position..(end < 0 ? markdown.Length : end)];

            if (fence is not null)
            {
                if (ClosesFence(line, fence)) fence = null;

                AppendBlank(builder, markdown, position, next);
            }
            else if (FenceOpen().Match(line) is { Success: true } opening)
            {
                fence = opening.Groups["fence"].Value;
                AppendBlank(builder, markdown, position, next);
            }
            else
            {
                builder.Append(markdown, position, next - position);
            }

            position = next;
        }

        return builder.ToString();
    }

    private static bool ClosesFence(string line, string fence)
    {
        var trimmed = line.TrimEnd('\r');
        var indent = trimmed.Length - trimmed.TrimStart(' ').Length;

        if (indent > 3) return false;

        var rest = trimmed[indent..].TrimEnd();

        return rest.Length >= fence.Length && rest.All(c => c == fence[0]);
    }

    // A code span runs from a run of backticks to the next run of the same length; runs of other
    // lengths inside it are text. Pairing each run with the next one of its length keeps this
    // linear. A lazy pattern rescanned to the end for every run that never closes, and runs of
    // distinct lengths never close each other.
    private static string BlankCodeSpans(string text)
    {
        var runs = BacktickRuns(text);
        var closers = new int[runs.Count];
        var nextOfLength = new Dictionary<int, int>();

        for (var i = runs.Count - 1; i >= 0; i--)
        {
            closers[i] = nextOfLength.GetValueOrDefault(runs[i].Length, -1);
            nextOfLength[runs[i].Length] = i;
        }

        char[]? blanked = null;
        var run = 0;

        while (run < runs.Count)
        {
            var closer = closers[run];

            if (closer < 0)
            {
                run++;
                continue;
            }

            blanked ??= text.ToCharArray();

            var end = runs[closer].Start + runs[closer].Length;

            for (var i = runs[run].Start; i < end; i++)
                if (blanked[i] is not ('\n' or '\r')) blanked[i] = ' ';

            run = closer + 1;
        }

        return blanked is null ? text : new string(blanked);
    }

    private static List<(int Start, int Length)> BacktickRuns(string text)
    {
        var runs = new List<(int Start, int Length)>();
        var start = text.IndexOf('`');

        while (start >= 0)
        {
            var end = start;

            while (end < text.Length && text[end] == '`') end++;

            runs.Add((start, end - start));
            start = end < text.Length ? text.IndexOf('`', end) : -1;
        }

        return runs;
    }

    private static void AppendBlank(StringBuilder builder, string source, int start, int end)
    {
        for (var i = start; i < end; i++)
            builder.Append(source[i] is '\n' or '\r' ? source[i] : ' ');
    }

    // The line holding `index` without its block markers and outer spaces, where that body starts
    // in the text, where the line ends, and the links on it when its snippets need cutting.
    private static (string Body, int BodyStart, int End, List<(int Start, int End)> Tokens) ReadLine(
        string markdown, int index)
    {
        var start = index == 0 ? 0 : markdown.LastIndexOf('\n', index - 1) + 1;
        var newline = markdown.IndexOf('\n', index);
        var end = newline < 0 ? markdown.Length : newline;
        var line = markdown[start..end].TrimEnd('\r');

        var prefix = BlockPrefix().Match(line).Length;
        var body = line[prefix..];
        var lead = body.Length - body.TrimStart().Length;

        body = body.Trim();

        var tokens = body.Length > SnippetLength
            ? LinkToken().Matches(body).Select(m => (Start: m.Index, End: m.Index + m.Length)).ToList()
            : [];

        return (body, start + prefix + lead, end, tokens);
    }

    // Cuts a long line around the link without splitting any link written on it, so every link in
    // the snippet still reads back as one.
    private static string Window(string line, int linkAt, List<(int Start, int End)> tokens)
    {
        if (line.Length <= SnippetLength) return line;

        var from = Math.Max(0, linkAt - SnippetLead);
        var to = Math.Min(line.Length, from + SnippetLength);

        from = Math.Max(0, to - SnippetLength);

        foreach (var (tokenStart, tokenEnd) in tokens)
        {
            if (from > tokenStart && from < tokenEnd) from = tokenStart;
            if (to > tokenStart && to < tokenEnd) to = tokenEnd;
        }

        from = SnapForward(line, from, linkAt, tokens);
        to = SnapBack(line, to, linkAt, tokens);

        // A long label can stretch the window across most of the line, but only the hard limit is
        // kept, so only that much is copied.
        var text = line.AsSpan(from, to - from).Trim();
        var builder = new StringBuilder(SnippetHardLimit + Ellipsis.Length);

        if (from > 0) builder.Append(Ellipsis);

        builder.Append(text[..Math.Min(text.Length, SnippetHardLimit)]);

        if (to < line.Length) builder.Append(Ellipsis);

        var snippet = builder.ToString();

        return snippet.Length <= SnippetHardLimit ? snippet : snippet[..SnippetHardLimit];
    }

    // Postgres can't store U+0000 in text, and Npgsql refuses half a surrogate pair, which a cut at a
    // character index can leave at either end of the window and editor text can carry in. Either
    // would fail the whole scan, so both are left out; a whole pair stays.
    private static string Storable(string snippet)
    {
        var span = snippet.AsSpan();

        if (span.IndexOf('\0') < 0 && span.IndexOfAnyInRange('\uD800', '\uDFFF') < 0) return snippet;

        var builder = new StringBuilder(snippet.Length);
        var i = 0;

        while (i < snippet.Length)
        {
            var c = snippet[i];

            if (char.IsHighSurrogate(c) && i + 1 < snippet.Length && char.IsLowSurrogate(snippet[i + 1]))
            {
                builder.Append(c).Append(snippet[i + 1]);
                i += 2;
                continue;
            }

            if (c != '\0' && !char.IsSurrogate(c)) builder.Append(c);

            i++;
        }

        return builder.ToString();
    }

    // Starts at the next word, unless that would skip into a link. Only a space before the link
    // counts: a link glued inside a long word has its next space after it, and starting there
    // would leave the link out or start the window past its end.
    private static int SnapForward(string line, int from, int linkAt, List<(int Start, int End)> tokens)
    {
        if (from == 0 || char.IsWhiteSpace(line[from - 1])) return from;
        if (tokens.Any(t => t.Start == from)) return from;

        var space = line.IndexOf(' ', from, Math.Max(0, linkAt - from));

        if (space < 0 || tokens.Any(t => space > t.Start && space < t.End)) return from;

        return space + 1;
    }

    // Ends after the last whole word, unless that would cut into a link. Only a space after the
    // link counts, for the same reason as above.
    private static int SnapBack(string line, int to, int linkAt, List<(int Start, int End)> tokens)
    {
        if (to == line.Length || char.IsWhiteSpace(line[to])) return to;
        if (tokens.Any(t => t.End == to)) return to;

        var space = line.LastIndexOf(' ', to - 1, Math.Max(0, to - 1 - linkAt));

        if (space < 0 || tokens.Any(t => space > t.Start && space < t.End)) return to;

        return space;
    }

    [GeneratedRegex(
        @"alexandria://(?<segment>file|folder|tag|playlist)/(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})")]
    private static partial Regex AnyLink();

    [GeneratedRegex(@"^ {0,3}(?<fence>`{3,}|~{3,})")]
    private static partial Regex FenceOpen();

    // Quote markers, a heading, a list bullet or number, and a task box.
    [GeneratedRegex(@"^\s*(?:>\s*)*(?:#{1,6}\s+|[-*+]\s+(?:\[[ xX]\]\s+)?|\d{1,9}[.)]\s+(?:\[[ xX]\]\s+)?)?")]
    private static partial Regex BlockPrefix();

    [GeneratedRegex(@"!?\[(?:\\.|[^\\\[\]\n])*\]\(alexandria://[^\s()<>]+\)|<alexandria://[^\s<>]+>|alexandria://[^\s<>()\[\]]+")]
    private static partial Regex LinkToken();
}
