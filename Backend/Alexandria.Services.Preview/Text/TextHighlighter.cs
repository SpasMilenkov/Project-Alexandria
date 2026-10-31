using System.Text;
using Alexandria.Dto.Files;
using Alexandria.Services.Preview.Text.Languages;
using Alexandria.Services.Preview.Text.Lexer;

namespace Alexandria.Services.Preview.Text;

public sealed record HighlightResult(
    IReadOnlyList<TextPreviewLineDto> Lines,
    int TokenCount,
    bool TokensLimited)
{
    public int LineCount => Lines.Count;

    public int DomElementCount => Lines.Count + TokenCount;
}

/// <summary>
/// Turns text into token ranges for a read-only preview. The server never builds markup:
/// <list type="bullet">
/// <item>Each line carries the exact text, including its trailing newline.</item>
/// <item>Token ranges index into that text and never include the newline.</item>
/// <item>The client renders the text as text nodes and wraps only the token ranges.</item>
/// </list>
/// Colors live in client CSS keyed on the token kind, so no color is baked into stored output.
/// </summary>
public static class TextHighlighter
{
    /// <summary>Text beyond this many characters is rendered as plain lines.</summary>
    public const int MaxHighlightChars = 200_000;

    /// <param name="maxTokenSpans">
    /// Maximum token spans to emit. Once reached, remaining text is emitted plain; this bounds
    /// the number of elements the client can render.
    /// </param>
    public static HighlightResult Highlight(
        string text,
        LanguageSpec language,
        int maxTokenSpans = int.MaxValue)
    {
        var normalized = Normalize(text);
        var lexer = new TextLexer(normalized, maxTokenSpans);
        lexer.Run(language);
        return lexer.Result;
    }

    /// <summary>Normalizes line breaks to \n and replaces control characters with U+FFFD.</summary>
    internal static string Normalize(string raw)
    {
        var clean = true;
        foreach (var c in raw)
            if (c == '\r' || (c < ' ' && c != '\t' && c != '\n') || c == '\u007f')
            {
                clean = false;
                break;
            }

        if (clean) return raw;

        var sb = new StringBuilder(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c == '\r')
            {
                sb.Append('\n');
                if (i + 1 < raw.Length && raw[i + 1] == '\n') i++;
            }
            else if ((c < ' ' && c != '\t' && c != '\n') || c == '\u007f') sb.Append('\uFFFD');
            else sb.Append(c);
        }

        return sb.ToString();
    }
}
