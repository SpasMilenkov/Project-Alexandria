namespace Alexandria.Services.Preview.Text.Lexer;

internal sealed partial class TextLexer
{
    // Markdown

    private void RunMarkdown(int i)
    {
        while (i < _end)
        {
            var le = LineEnd(i);

            if (le - i > MaxLineChars)
            {
                Emit(null, i, le);
                Break(le);
                i = Next(le);
                continue;
            }

            if (TryFence(i, le, out var fenceChar, out var fenceLength, out var info))
            {
                Emit(Mt, i, le);
                Break(le);

                var contentStart = Next(le);
                var closeStart = -1;
                for (var p = contentStart; p < _end;)
                {
                    var pe = LineEnd(p);
                    if (IsClosingFence(p, pe, fenceChar, fenceLength))
                    {
                        closeStart = p;
                        break;
                    }

                    p = Next(pe);
                }

                var contentEnd = closeStart >= 0 ? closeStart : _end;
                var codeEnd = contentEnd;
                if (codeEnd > contentStart && _text[codeEnd - 1] == '\n') codeEnd--;

                if (codeEnd > contentStart) RunRegion(PreviewLanguages.ByName(info), contentStart, codeEnd);
                if (contentEnd > codeEnd) Emit(null, codeEnd, contentEnd);

                if (closeStart >= 0)
                {
                    var closeEnd = LineEnd(closeStart);
                    Emit(Mt, closeStart, closeEnd);
                    Break(closeEnd);
                    i = Next(closeEnd);
                }
                else i = _end;

                continue;
            }

            EmitMarkdownLine(i, le);
            Break(le);
            i = Next(le);
        }
    }

    private bool TryFence(int i, int le, out char fenceChar, out int fenceLength, out string info)
    {
        fenceChar = '\0';
        fenceLength = 0;
        info = "";

        var s = i;
        while (s < le && s - i < 3 && _text[s] == ' ') s++;
        if (s >= le || (_text[s] != '`' && _text[s] != '~')) return false;

        fenceChar = _text[s];
        var k = s;
        while (k < le && _text[k] == fenceChar) k++;
        fenceLength = k - s;
        if (fenceLength < 3) return false;

        info = _text.Substring(k, le - k).Trim();
        var space = info.IndexOf(' ');
        if (space > 0) info = info[..space];
        return true;
    }

    private bool IsClosingFence(int p, int pe, char fenceChar, int fenceLength)
    {
        var s = p;
        while (s < pe && s - p < 3 && _text[s] == ' ') s++;
        var k = s;
        while (k < pe && _text[k] == fenceChar) k++;
        if (k - s < fenceLength) return false;

        for (var m = k; m < pe; m++)
            if (_text[m] != ' ' && _text[m] != '\t')
                return false;
        return true;
    }

    private void EmitMarkdownLine(int i, int le)
    {
        var s = i;
        while (s < le && _text[s] == ' ') s++;

        if (s < le && _text[s] == '#')
        {
            var k = s;
            while (k < le && _text[k] == '#') k++;
            if (k - s <= 6 && (k == le || _text[k] == ' '))
            {
                Emit(Hd, i, le);
                return;
            }
        }

        if (s < le && _text[s] == '>')
        {
            Emit(Cm, i, le);
            return;
        }

        if (IsHorizontalRule(s, le))
        {
            Emit(Mt, i, le);
            return;
        }

        var inline = s;
        Emit(null, i, s);

        if (s < le)
        {
            var k = s;
            if ((_text[k] is '-' or '*' or '+') && k + 1 < le && _text[k + 1] == ' ') k++;
            else
            {
                while (k < le && IsAsciiDigit(_text[k])) k++;
                if (k > s && k + 1 < le && (_text[k] == '.' || _text[k] == ')') && _text[k + 1] == ' ') k++;
                else k = s;
            }

            if (k > s)
            {
                Emit(Mt, s, k);
                inline = k;
            }
        }

        EmitInlineMarkdown(inline, le);
    }

    private bool IsHorizontalRule(int s, int le)
    {
        if (s >= le || (_text[s] != '-' && _text[s] != '*' && _text[s] != '_')) return false;

        var marker = _text[s];
        var count = 0;
        for (var k = s; k < le; k++)
        {
            if (_text[k] == marker) count++;
            else if (_text[k] != ' ') return false;
        }

        return count >= 3;
    }

    private void EmitInlineMarkdown(int s, int e)
    {
        var plainStart = s;
        var i = s;

        while (i < e)
        {
            var c = _text[i];

            if (c == '`')
            {
                var close = _text.IndexOf('`', i + 1, e - (i + 1));
                if (close > 0)
                {
                    Emit(null, plainStart, i);
                    Emit(St, i, close + 1);
                    i = plainStart = close + 1;
                    continue;
                }
            }
            else if (c == '*' && i + 1 < e && _text[i + 1] == '*')
            {
                var close = _text.IndexOf("**", i + 2, e - (i + 2), StringComparison.Ordinal);
                if (close > 0)
                {
                    Emit(null, plainStart, i);
                    Emit(Bd, i, close + 2);
                    i = plainStart = close + 2;
                    continue;
                }
            }
            else if (c == '[')
            {
                var mid = _text.IndexOf("](", i + 1, e - (i + 1), StringComparison.Ordinal);
                if (mid > 0)
                {
                    var close = _text.IndexOf(')', mid + 2, e - (mid + 2));
                    if (close > 0)
                    {
                        Emit(null, plainStart, i);
                        Emit(Fn, i, mid + 1);
                        Emit(null, mid + 1, mid + 2);
                        Emit(St, mid + 2, close);
                        Emit(null, close, close + 1);
                        i = plainStart = close + 1;
                        continue;
                    }
                }
            }

            i++;
        }

        Emit(null, plainStart, e);
    }
}
