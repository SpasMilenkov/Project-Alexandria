using System.Text.RegularExpressions;

namespace Alexandria.Services.Storage.Notes;

//TODO: I should probably simplify this


/// <summary>
/// Reads the name an untitled note should carry from its text, the way a word processor names a
/// new document after its first line: the first heading or line with words in it, with the
/// markdown around those words taken off. The line only counts once something follows it, so a
/// save during a pause mid-heading never names the note after half a title.
/// </summary>
internal static partial class NoteTitles
{
    // Long enough for a real heading, short enough to read in a file list.
    public const int MaxLength = 80;

    /// <summary>The note's title, or null when the text has no finished line with words in it yet.</summary>
    public static string? FromMarkdown(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return null;

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var inFence = false;

        for (var i = FirstLineAfterFrontmatter(lines); i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Code blocks hold code, not a title, so the whole block is passed over.
            if (FencePattern().IsMatch(line))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence || line.Length == 0 || RulePattern().IsMatch(line)) continue;

            var title = Shorten(PlainText(line));

            if (title.Length == 0) continue;

            // Still the line being typed: nothing has been written after it yet.
            if (!HasTextAfter(lines, i)) return null;

            return title;
        }

        return null;
    }

    // Frontmatter holds metadata (Obsidian puts tags and aliases there), never the title line.
    private static int FirstLineAfterFrontmatter(string[] lines)
    {
        if (lines.Length == 0 || lines[0].Trim() != "---") return 0;

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line is "---" or "...") return i + 1;
        }

        // An opening fence with no closing one is just a rule followed by text.
        return 0;
    }

    private static bool HasTextAfter(string[] lines, int index)
    {
        for (var i = index + 1; i < lines.Length; i++)
        {
            if (lines[i].Trim().Length > 0) return true;
        }

        return false;
    }

    private static string PlainText(string line)
    {
        var text = BlockPrefixPattern().Replace(line, string.Empty);
        text = ClosingHashesPattern().Replace(text, string.Empty);
        text = ImagePattern().Replace(text, "$1");
        text = LinkPattern().Replace(text, "$1");
        text = AutolinkPattern().Replace(text, string.Empty);
        text = HtmlTagPattern().Replace(text, string.Empty);
        text = MarkerPattern().Replace(text, string.Empty);

        return WhitespacePattern().Replace(text, " ").Trim();
    }

    // Cut at a word boundary so the name never ends halfway through a word.
    private static string Shorten(string text)
    {
        if (text.Length <= MaxLength) return text;

        var cut = text[..MaxLength];
        var lastSpace = cut.LastIndexOf(' ');

        return (lastSpace > MaxLength / 2 ? cut[..lastSpace] : cut).TrimEnd();
    }

    [GeneratedRegex(@"^(```|~~~)")]
    private static partial Regex FencePattern();

    // Thematic breaks and setext underlines carry no words.
    [GeneratedRegex(@"^([-*_=])(\s*\1){2,}$")]
    private static partial Regex RulePattern();

    // Heading hashes, quote markers, list bullets and numbers, and task boxes, in any nesting.
    [GeneratedRegex(@"^((#{1,6}|>|[-*+]|\d+[.)])\s+|\[[ xX]\]\s+)+")]
    private static partial Regex BlockPrefixPattern();

    [GeneratedRegex(@"\s+#+$")]
    private static partial Regex ClosingHashesPattern();

    [GeneratedRegex(@"!\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex ImagePattern();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"<[a-zA-Z][a-zA-Z0-9+.-]*:[^>\s]*>")]
    private static partial Regex AutolinkPattern();

    [GeneratedRegex(@"</?[a-zA-Z][^>]*>")]
    private static partial Regex HtmlTagPattern();

    // Emphasis, strikethrough and code ticks around words. Underscores inside a word, as in
    // snake_case, are part of the word and stay.
    [GeneratedRegex(@"[*~`]+|(?<![\p{L}\p{N}])_+|_+(?![\p{L}\p{N}])")]
    private static partial Regex MarkerPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
