namespace Alexandria.Services.Preview.Text.Lexer;

internal sealed partial class TextLexer
{
    // Markup (XML, HTML, Vue)

    private void RunMarkup(int i)
    {
        while (i < _end)
        {
            var c = _text[i];

            if (c == '\n')
            {
                Emit(null, i, i + 1);
                i++;
                continue;
            }

            if (i - _lineStart > MaxLineChars)
            {
                i = EmitRestOfLine(i);
                continue;
            }

            if (c == '<')
            {
                if (Match(i, "<!--"))
                {
                    var index = _text.IndexOf("-->", i + 4, _end - (i + 4), StringComparison.Ordinal);
                    var stop = index < 0 ? _end : index + 3;
                    Emit(Cm, i, stop);
                    i = stop;
                    continue;
                }

                if (i + 1 < _end && (_text[i + 1] == '!' || _text[i + 1] == '?'))
                {
                    var gt = _text.IndexOf('>', i, _end - i);
                    var stop = gt < 0 ? _end : gt + 1;
                    Emit(Mt, i, stop);
                    i = stop;
                    continue;
                }

                if (i + 1 < _end && (_text[i + 1] == '/' || IsAsciiLetter(_text[i + 1])))
                {
                    i = EmitTag(i);
                    continue;
                }
            }

            var next = i + 1;
            while (next < _end && _text[next] != '<' && _text[next] != '\n') next++;
            Emit(null, i, next);
            i = next;
        }
    }

    private static bool IsAttributeChar(char c)
        => char.IsLetterOrDigit(c) || c is '-' or '_' or ':' or '.' or '@' or '#';

    private int EmitTag(int i)
    {
        var j = i + 1;
        var closing = false;
        if (_text[j] == '/')
        {
            closing = true;
            j++;
        }

        Emit(null, i, j);

        var nameStart = j;
        while (j < _end && (IsIdentChar(_text[j]) || _text[j] is '-' or ':' or '.')) j++;
        var name = _text.Substring(nameStart, j - nameStart);
        Emit(Tg, nameStart, j);

        var selfClosing = false;
        var closed = false;

        while (j < _end)
        {
            var c = _text[j];

            if (c == '>')
            {
                Emit(null, j, j + 1);
                j++;
                closed = true;
                break;
            }

            if (c == '<') break;

            if (c == '\n')
            {
                Emit(null, j, j + 1);
                j++;
                continue;
            }

            if (c == ' ' || c == '\t')
            {
                var k = j + 1;
                while (k < _end && (_text[k] == ' ' || _text[k] == '\t')) k++;
                Emit(null, j, k);
                j = k;
                continue;
            }

            if (c == '/')
            {
                selfClosing = j + 1 < _end && _text[j + 1] == '>';
                Emit(null, j, j + 1);
                j++;
                continue;
            }

            if (c == '"' || c == '\'')
            {
                var close = _text.IndexOf(c, j + 1, _end - (j + 1));
                var stop = close < 0 ? _end : close + 1;
                Emit(St, j, stop);
                j = stop;
                continue;
            }

            if (IsAttributeChar(c))
            {
                var k = j + 1;
                while (k < _end && IsAttributeChar(_text[k])) k++;
                Emit(At, j, k);
                j = k;
                continue;
            }

            Emit(null, j, j + 1);
            j++;
        }

        var isScript = name.Equals("script", StringComparison.OrdinalIgnoreCase);
        var isStyle = name.Equals("style", StringComparison.OrdinalIgnoreCase);

        if (closed && !closing && !selfClosing && (isScript || isStyle))
        {
            var closeIndex = _text.IndexOf("</" + name, j, _end - j, StringComparison.OrdinalIgnoreCase);
            var stop = closeIndex < 0 ? _end : closeIndex;
            if (stop > j)
            {
                RunRegion(isScript ? PreviewLanguages.TypeScript : PreviewLanguages.Css, j, stop);
                j = stop;
            }
        }

        return j;
    }
}
