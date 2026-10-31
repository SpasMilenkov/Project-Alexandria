using System.Text;
using Alexandria.Dto.Files;
using Alexandria.Services.Preview.Text.Languages;

namespace Alexandria.Services.Preview.Text.Lexer;

internal sealed partial class TextLexer
{
    private const string Kw = "kw", St = "st", Cm = "cm", Nu = "nu", Li = "li", Fn = "fn", Ty = "ty";
    private const string Ky = "ky", Tg = "tg", At = "at", Mt = "mt", Hd = "hd", Bd = "bd";
    private const int MaxHighlightChars = TextHighlighter.MaxHighlightChars;
    private const int MaxLineChars = 3_000;

    private readonly string _text;
    private readonly int _maxTokenSpans;
    private readonly List<LineBuilder> _lines = [];
    private LineBuilder? _currentLine;
    private int _lineStart;
    private LanguageSpec _spec = PreviewLanguages.Plain;
    private int _end;
    private int tokenCount;
    private bool tokensLimited;

    public TextLexer(string text, int maxTokenSpans)
    {
        this._text = text;
        this._maxTokenSpans = Math.Max(0, maxTokenSpans);
    }

    public HighlightResult Result => new(
        _lines.Select(line => new TextPreviewLineDto(line.Text.ToString(), line.Tokens.ToArray())).ToArray(),
        tokenCount,
        tokensLimited);

    public void Run(LanguageSpec language)
    {
        var limit = _text.Length;
        if (_text.Length > MaxHighlightChars)
        {
            var cut = _text.LastIndexOf('\n', MaxHighlightChars);
            limit = cut >= 0 ? cut + 1 : MaxHighlightChars;
        }

        RunRegion(language, 0, limit);
        if (limit < _text.Length) Emit(null, limit, _text.Length);

        if (_currentLine is not null) EndLine();
    }

    private void RunRegion(LanguageSpec s, int from, int to)
    {
        var savedSpec = _spec;
        var savedEnd = _end;
        _spec = s;
        _end = to;

        switch (s.Mode)
        {
            case HighlightMode.Code: RunCode(from); break;
            case HighlightMode.Markup: RunMarkup(from); break;
            case HighlightMode.Markdown: RunMarkdown(from); break;
            case HighlightMode.Diff: RunDiff(from); break;
            case HighlightMode.Subtitle: RunSubtitle(from); break;
            default: Emit(null, from, to); break;
        }

        _spec = savedSpec;
        _end = savedEnd;
    }

    // Writer

    private void OpenLine()
    {
        if (_currentLine is not null) return;
        _currentLine = new LineBuilder();
        _lines.Add(_currentLine);
    }

    private void EndLine()
    {
        OpenLine();
        _currentLine!.Text.Append('\n');
        _currentLine = null;
    }

    /// <summary>Adds text[from..to) with an optional token kind, splitting at line breaks.</summary>
    private void Emit(string? cls, int from, int to)
    {
        var i = from;
        while (i < to)
        {
            var nl = _text.IndexOf('\n', i, to - i);
            var segmentEnd = nl < 0 ? to : nl;

            if (segmentEnd > i)
            {
                if (cls is null) AppendText(i, segmentEnd);
                else AppendToken(i, segmentEnd, cls);
            }

            if (nl < 0) break;

            EndLine();
            _lineStart = nl + 1;
            i = nl + 1;
        }
    }

    private void AppendText(int from, int to)
    {
        if (to <= from) return;

        OpenLine();
        _currentLine!.Text.Append(_text, from, to - from);
    }

    private void AppendToken(int from, int to, string cls)
    {
        if (to <= from) return;

        if (tokenCount >= _maxTokenSpans)
        {
            tokensLimited = true;
            AppendText(from, to);
            return;
        }

        OpenLine();
        var start = _currentLine!.Text.Length;
        _currentLine.Text.Append(_text, from, to - from);
        _currentLine.Tokens.Add(new TextPreviewTokenDto(start, to - from, cls));
        tokenCount++;
    }

    private sealed class LineBuilder
    {
        public StringBuilder Text { get; } = new();
        public List<TextPreviewTokenDto> Tokens { get; } = [];
    }

    // Helpers

    private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';
    private static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    private static bool IsHex(char c) => IsAsciiDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private bool Match(int i, string s)
        => i + s.Length <= _end && string.CompareOrdinal(_text, i, s, 0, s.Length) == 0;

    private int LineEnd(int i)
    {
        var nl = _text.IndexOf('\n', i, _end - i);
        return nl < 0 ? _end : nl;
    }

    private int Next(int lineEnd) => lineEnd < _end ? lineEnd + 1 : _end;

    private void Break(int lineEnd)
    {
        if (lineEnd < _end) Emit(null, lineEnd, lineEnd + 1);
    }

    private bool OnlyBlankBefore(int i)
    {
        for (var k = i - 1; k >= _lineStart; k--)
            if (_text[k] != ' ' && _text[k] != '\t')
                return false;
        return true;
    }

    private int EmitRestOfLine(int i)
    {
        var eol = LineEnd(i);
        Emit(null, i, eol);
        return eol;
    }
}
