using System.Text;
using System.Text.RegularExpressions;

namespace Alexandria.Services.Storage.Notes;

/// <summary>
/// The opening words of a note for its row on the Notes page: the text after the title, with
/// frontmatter, code blocks and line markers (headings, quotes, bullets, task boxes) left out.
/// Links stay as written, whole, so the page shows them by name.
/// </summary>
internal static partial class NoteExcerpts
{
    // About two lines of a list row; the page clamps whatever it is given to two.
    public const int MaxLength = 220;

    // How much of the stored text is read for it; plenty for 220 characters after a title.
    public const int SourceLength = 2000;

    private const string Ellipsis = "…";

    /// <summary>The excerpt, or null when nothing follows the title.</summary>
    public static string? From(string text, string fileName)
    {
        var title = Comparable(Path.GetFileNameWithoutExtension(fileName));
        var parts = new List<string>();
        var length = 0;
        var isFirst = true;
        var inFence = false;

        foreach (var raw in BodyLines(text))
        {
            var line = raw.Trim();

            if (Fence().IsMatch(line))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence || line.Length == 0 || Rule().IsMatch(line)) continue;

            var body = BlockPrefix().Replace(line, "").Trim();

            if (body.Length == 0) continue;

            // The first line is the title when it is a heading or reads like the note's name,
            // which is how "Untitled" notes get their names.
            if (isFirst)
            {
                isFirst = false;

                if (line.StartsWith('#') || Comparable(body) == title) continue;
            }

            parts.Add(body);
            length += body.Length + 1;

            if (length > MaxLength * 2) break;
        }

        if (parts.Count == 0) return null;

        return Cut(string.Join(' ', parts));
    }

    private static IEnumerable<string> BodyLines(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\f', '\n').Split('\n');
        var start = 0;

        // Frontmatter is metadata (Obsidian keeps tags and aliases there), not text to read.
        if (lines.Length > 0 && lines[0].Trim() == "---")
        {
            var end = Array.FindIndex(lines, 1, line => line.Trim() == "---");

            if (end > 0) start = end + 1;
        }

        return lines.Skip(start);
    }

    // Names and lines compare without case and without the markers emphasis adds around words.
    private static string Comparable(string value) =>
        Markers().Replace(value, "").Trim().ToLowerInvariant();

    // Cuts at a word, never inside a link, so every link in the excerpt still reads as one.
    private static string Cut(string text)
    {
        if (text.Length <= MaxLength) return text;

        var cut = MaxLength;

        foreach (Match token in LinkToken().Matches(text))
        {
            if (cut > token.Index && cut < token.Index + token.Length)
            {
                cut = token.Index + token.Length;
                break;
            }
        }

        if (cut == MaxLength)
        {
            var space = text.LastIndexOf(' ', cut - 1);

            if (space > MaxLength / 2) cut = space;
        }

        if (cut >= text.Length) return text;

        return new StringBuilder(text[..cut].TrimEnd()).Append(Ellipsis).ToString();
    }

    [GeneratedRegex(@"^(`{3,}|~{3,})")]
    private static partial Regex Fence();

    [GeneratedRegex(@"^([-*_])(\s*\1){2,}$")]
    private static partial Regex Rule();

    // Quote markers, a heading, a list bullet or number, and a task box.
    [GeneratedRegex(@"^(?:>\s*)*(?:#{1,6}\s+|[-*+]\s+(?:\[[ xX]\]\s+)?|\d{1,9}[.)]\s+(?:\[[ xX]\]\s+)?)?")]
    private static partial Regex BlockPrefix();

    [GeneratedRegex(@"[*_~`#]")]
    private static partial Regex Markers();

    [GeneratedRegex(@"!?\[(?:\\.|[^\\\[\]\n])*\]\(alexandria://[^\s()<>]+\)|<alexandria://[^\s<>]+>")]
    private static partial Regex LinkToken();
}
