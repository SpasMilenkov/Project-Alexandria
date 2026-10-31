namespace Alexandria.Services.Preview.Text.Lexer;

internal sealed partial class TextLexer
{
    // Diff

    private void RunDiff(int i)
    {
        while (i < _end)
        {
            var le = LineEnd(i);
            Emit(DiffLineClass(i, le), i, le);
            Break(le);
            i = Next(le);
        }
    }

    private string? DiffLineClass(int i, int le)
    {
        if (i >= le) return null;

        if (Match(i, "+++") || Match(i, "---")) return Mt;
        if (Match(i, "@@")) return Hd;
        if (Match(i, "\\ No newline")) return Cm;

        if (Match(i, "diff ") || Match(i, "index ") || Match(i, "new file mode") ||
            Match(i, "deleted file mode") || Match(i, "similarity index") ||
            Match(i, "rename from") || Match(i, "rename to") || Match(i, "Binary files"))
            return Mt;

        if (_text[i] == '+') return St;
        if (_text[i] == '-') return Kw;

        return null;
    }

    // Subtitles

    private void RunSubtitle(int i)
    {
        while (i < _end)
        {
            var le = LineEnd(i);
            Emit(SubtitleLineClass(i, le), i, le);
            Break(le);
            i = Next(le);
        }
    }

    private string? SubtitleLineClass(int i, int le)
    {
        var start = i;
        while (start < le && (_text[start] == ' ' || _text[start] == '\t')) start++;
        if (start >= le) return null;

        if (IsMarker(start, le, "WEBVTT")) return Kw;
        if (IsMarker(start, le, "NOTE")) return Cm;
        if (IsMarker(start, le, "STYLE")) return Kw;
        if (IsMarker(start, le, "REGION")) return Kw;

        if (_text.IndexOf("-->", start, le - start, StringComparison.Ordinal) >= 0) return Mt;

        var digits = true;
        for (var k = start; k < le; k++)
        {
            if (!IsAsciiDigit(_text[k]))
            {
                digits = false;
                break;
            }
        }

        return digits ? Nu : null;
    }

    private bool IsMarker(int start, int le, string marker)
        => Match(start, marker) &&
           (start + marker.Length >= le || char.IsWhiteSpace(_text[start + marker.Length]));
}
