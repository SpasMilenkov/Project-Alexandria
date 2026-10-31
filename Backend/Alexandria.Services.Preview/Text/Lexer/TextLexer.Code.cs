namespace Alexandria.Services.Preview.Text.Lexer;

internal sealed partial class TextLexer
{
    // Code

    private void RunCode(int i)
    {
        var depth = 0;

        while (i < _end)
        {
            var c = _text[i];

            if (c == '\n')
            {
                Emit(null, i, i + 1);
                i++;
                continue;
            }

            if (_spec.Delimiters.Length > 0 && _spec.Delimiters.Contains(c))
            {
                Emit(Mt, i, i + 1);
                i++;
                continue;
            }

            if (c == ' ' || c == '\t')
            {
                var j = i + 1;
                while (j < _end && (_text[j] == ' ' || _text[j] == '\t')) j++;
                Emit(null, i, j);
                i = j;
                continue;
            }

            if (i - _lineStart > MaxLineChars)
            {
                i = EmitRestOfLine(i);
                continue;
            }

            if (TryComment(ref i)) continue;

            if (_spec.HashDirectives && c == '#' && OnlyBlankBefore(i))
            {
                var k = i + 1;
                while (k < _end && IsAsciiLetter(_text[k])) k++;
                if (k > i + 1)
                {
                    Emit(Mt, i, k);
                    i = k;
                    continue;
                }
            }

            if (_spec.HashColors && c == '#')
            {
                var k = i + 1;
                while (k < _end && IsHex(_text[k])) k++;
                var length = k - i - 1;
                if ((length is 3 or 4 or 6 or 8) && (k >= _end || !IsIdentChar(_text[k])))
                {
                    Emit(Nu, i, k);
                    i = k;
                    continue;
                }
            }

            if (_spec.AtKeywords && c == '@' && i + 1 < _end && IsIdentStart(_text[i + 1]))
            {
                var k = i + 1;
                while (k < _end && (IsIdentChar(_text[k]) || _text[k] == '-')) k++;
                Emit(Kw, i, k);
                i = k;
                continue;
            }

            if (_spec.Sections && c == '[' && OnlyBlankBefore(i))
            {
                var eol = LineEnd(i);
                var close = eol > i ? _text.LastIndexOf(']', eol - 1, eol - i) : -1;
                if (close > i)
                {
                    Emit(Mt, i, close + 1);
                    i = close + 1;
                    continue;
                }
            }

            if (_spec.Variables && c == '$')
            {
                var k = ScanVariable(i);
                if (k > i + 1)
                {
                    Emit(At, i, k);
                    i = k;
                    continue;
                }
            }

            if (_spec.PercentVariables && c == '%')
            {
                var k = ScanPercentVariable(i);
                if (k > i + 1)
                {
                    Emit(At, i, k);
                    i = k;
                    continue;
                }
            }

            var stringEnd = TryString(i);
            if (stringEnd > i)
            {
                var isKey = _spec.KeyTerminators.Length > 0 && FollowedByTerminator(stringEnd, strict: false);
                Emit(isKey ? Ky : St, i, stringEnd);
                i = stringEnd;
                continue;
            }

            if (IsAsciiDigit(c))
            {
                var k = ScanNumber(i);
                Emit(Nu, i, k);
                i = k;
                continue;
            }

            if (IsIdentStart(c))
            {
                i = EmitIdentifier(i, depth);
                continue;
            }

            if (c == '{') depth++;
            else if (c == '}' && depth > 0) depth--;

            Emit(null, i, i + 1);
            i++;
        }
    }

    private bool TryComment(ref int i)
    {
        // Block openers are checked first because they can share a prefix with a line comment
        // marker, for example Julia's "#=" against "#".
        foreach (var (open, close) in _spec.BlockComments)
        {
            if (!Match(i, open)) continue;

            var from = i + open.Length;
            var index = _text.IndexOf(close, from, _end - from, StringComparison.Ordinal);
            var stop = index < 0 ? _end : index + close.Length;
            Emit(Cm, i, stop);
            i = stop;
            return true;
        }

        foreach (var lineComment in _spec.LineComments)
        {
            if (!Match(i, lineComment)) continue;
            if (_spec.HashNeedsBoundary && lineComment == "#" && i > _lineStart && !char.IsWhiteSpace(_text[i - 1]))
                continue;

            var eol = LineEnd(i);
            Emit(Cm, i, eol);
            i = eol;
            return true;
        }

        return false;
    }
}
