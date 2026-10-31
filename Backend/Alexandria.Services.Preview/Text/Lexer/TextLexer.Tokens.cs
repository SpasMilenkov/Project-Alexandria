namespace Alexandria.Services.Preview.Text.Lexer;

internal sealed partial class TextLexer
{
    /// <summary>Returns the end of a string starting at i, or -1 when no string starts here.</summary>
    private int TryString(int i)
    {
        var c = _text[i];

        if (_spec.CSharpStrings && (c == '$' || c == '@'))
        {
            var j = i;
            var verbatim = false;
            while (j < _end && (_text[j] == '$' || _text[j] == '@') && j - i < 3)
            {
                if (_text[j] == '@') verbatim = true;
                j++;
            }

            if (j < _end && _text[j] == '"' && !(_spec.TripleQuotes && Match(j, "\"\"\"")))
                return verbatim ? ScanQuoted(j, '"', escapes: false, multiline: true, doubled: true)
                                : ScanQuoted(j, '"', escapes: true, multiline: false, doubled: false);
            return -1;
        }

        if (_spec.TripleQuotes && (c == '"' || c == '\'') && Match(i, new string(c, 3)))
        {
            var closer = new string(c, 3);
            var from = i + 3;
            var index = _text.IndexOf(closer, from, _end - from, StringComparison.Ordinal);
            return index < 0 ? _end : index + 3;
        }

        if (_spec.RawQuotes.Contains(c)) return ScanQuoted(i, c, escapes: false, multiline: false, doubled: false);
        if (_spec.MultilineQuotes.Contains(c)) return ScanQuoted(i, c, escapes: true, multiline: true, doubled: false);

        if (_spec.Quotes.Contains(c))
        {
            if (c == '\'' && _spec.ApostropheNeedsBoundary && i > _lineStart && IsIdentChar(_text[i - 1])) return -1;
            return ScanQuoted(i, c, _spec.BackslashEscapes, multiline: false, _spec.DoubledQuotes);
        }

        return -1;
    }

    private int ScanQuoted(int i, char quote, bool escapes, bool multiline, bool doubled)
    {
        var j = i + 1;
        while (j < _end)
        {
            var d = _text[j];

            // An unterminated single-line string stops at the end of the line
            if (d == '\n' && !multiline) return j;

            if (escapes && d == '\\' && j + 1 < _end && _text[j + 1] != '\n')
            {
                j += 2;
                continue;
            }

            if (d == quote)
            {
                if (doubled && j + 1 < _end && _text[j + 1] == quote)
                {
                    j += 2;
                    continue;
                }

                return j + 1;
            }

            j++;
        }

        return _end;
    }

    private int ScanNumber(int i)
    {
        var j = i;

        if (_text[j] == '0' && j + 1 < _end && "xXbBoO".Contains(_text[j + 1]))
        {
            j += 2;
            while (j < _end && (IsHex(_text[j]) || _text[j] == '_')) j++;
        }
        else
        {
            while (j < _end && (IsAsciiDigit(_text[j]) || _text[j] == '_')) j++;

            if (j + 1 < _end && _text[j] == '.' && IsAsciiDigit(_text[j + 1]))
            {
                j++;
                while (j < _end && (IsAsciiDigit(_text[j]) || _text[j] == '_')) j++;
            }

            if (j < _end && (_text[j] == 'e' || _text[j] == 'E'))
            {
                var k = j + 1;
                if (k < _end && (_text[k] == '+' || _text[k] == '-')) k++;
                if (k < _end && IsAsciiDigit(_text[k]))
                {
                    j = k;
                    while (j < _end && IsAsciiDigit(_text[j])) j++;
                }
            }
        }

        // Suffixes such as f, L, ul, px and %
        while (j < _end && (IsAsciiLetter(_text[j]) || _text[j] == '%')) j++;

        return j;
    }

    private int ScanVariable(int i)
    {
        var j = i + 1;
        if (j >= _end) return i + 1;

        if (_text[j] == '{')
        {
            var close = _text.IndexOf('}', j, LineEnd(j) - j);
            return close < 0 ? i + 1 : close + 1;
        }

        if (_text[j] == '(' && _spec.ParenthesizedVariables)
        {
            var close = _text.IndexOf(')', j, LineEnd(j) - j);
            return close < 0 ? i + 1 : close + 1;
        }

        if (IsIdentStart(_text[j]))
        {
            while (j < _end && IsIdentChar(_text[j])) j++;
            return j;
        }

        return IsAsciiDigit(_text[j]) || "?@#$!*-".Contains(_text[j]) ? j + 1 : i + 1;
    }

    private int ScanPercentVariable(int i)
    {
        var j = i + 1;
        if (j >= _end) return i + 1;

        var eol = LineEnd(j);

        if (_text[j] == '%' || IsAsciiDigit(_text[j])) return j + 1;

        if (_text[j] == '{')
        {
            var close = _text.IndexOf('}', j, eol - j);
            return close < 0 ? i + 1 : close + 1;
        }

        if (_text[j] == '~')
        {
            var k = j + 1;
            while (k < eol && _text[k] != '%' && !char.IsWhiteSpace(_text[k])) k++;
            return k;
        }

        var end = _text.IndexOf('%', j, eol - j);
        return end < 0 ? i + 1 : end + 1;
    }

    private int ScanIdent(int i)
    {
        var j = i + 1;
        while (j < _end)
        {
            var c = _text[j];
            if (IsIdentChar(c))
            {
                j++;
                continue;
            }

            if (_spec.HyphenIdents && c == '-' && j + 1 < _end && IsIdentChar(_text[j + 1]))
            {
                j++;
                continue;
            }

            break;
        }

        return j;
    }

    private bool FollowedByTerminator(int pos, bool strict)
    {
        while (pos < _end && (_text[pos] == ' ' || _text[pos] == '\t')) pos++;
        if (pos >= _end || !_spec.KeyTerminators.Contains(_text[pos])) return false;

        var terminator = _text[pos];
        var after = pos + 1 < _end ? _text[pos + 1] : '\n';

        if ((terminator == ':' && after == ':') || (terminator == '=' && after == '=')) return false;
        if (strict && _spec.StrictKeyColon && terminator == ':' && after != ' ' && after != '\t' && after != '\n')
            return false;

        return true;
    }

    private int EmitIdentifier(int i, int depth)
    {
        var j = ScanIdent(i);

        if (_spec.StringPrefixes && j - i <= 2 && j < _end && (_text[j] == '"' || _text[j] == '\'') &&
            IsStringPrefix(_text.AsSpan(i, j - i)))
        {
            var stringEnd = TryString(j);
            if (stringEnd > j)
            {
                Emit(St, i, stringEnd);
                return stringEnd;
            }
        }

        var word = _text.Substring(i, j - i);
        string? cls = null;

        if (_spec.Keywords.Contains(word)) cls = Kw;
        else if (_spec.Literals.Contains(word)) cls = Li;
        else if (_spec.KeyTerminators.Length > 0 && (!_spec.KeysInBraces || depth > 0) &&
                 FollowedByTerminator(j, strict: true)) cls = Ky;
        else if (_spec.FnHeuristic && j < _end && _text[j] == '(') cls = Fn;
        else if (_spec.TypeHeuristic && IsPascalCase(word)) cls = Ty;

        Emit(cls, i, j);
        return j;
    }

    private static bool IsStringPrefix(ReadOnlySpan<char> word)
    {
        foreach (var c in word)
            if ("rbfuRBFU".IndexOf(c) < 0)
                return false;
        return word.Length > 0;
    }

    private static bool IsPascalCase(string word)
    {
        if (word.Length < 2 || !char.IsUpper(word[0])) return false;
        foreach (var c in word)
            if (char.IsLower(c))
                return true;
        return false;
    }
}
