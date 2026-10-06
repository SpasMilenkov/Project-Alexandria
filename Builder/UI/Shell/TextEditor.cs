using System.Text;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell;

internal sealed class TextEditor(string initial, bool secret) : IRenderable
{
    private const string Cursor = "▏";
    private char? _pendingHighSurrogate;

    public string Value { get; private set; } = initial;
    public int Position { get; private set; } = initial.Length;

    public bool HandleKey(ConsoleKeyInfo key)
    {
        var pending = _pendingHighSurrogate;
        _pendingHighSurrogate = null;

        if (key.Modifiers.HasFlag(ConsoleModifiers.Control) &&
            !key.Modifiers.HasFlag(ConsoleModifiers.Alt))
        {
            if (key.Key == ConsoleKey.U)
            {
                Value = string.Empty;
                Position = 0;

                return true;
            }

            return false;
        }

        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
                Position = PreviousPosition(Position);

                return false;
            case ConsoleKey.RightArrow:
                Position = NextPosition(Position);

                return false;
            case ConsoleKey.Home:
                Position = 0;

                return false;
            case ConsoleKey.End:
                Position = Value.Length;

                return false;
            case ConsoleKey.Backspace when Position > 0:
                var previous = PreviousPosition(Position);
                Value = Value.Remove(previous, Position - previous);
                Position = previous;

                return true;
            case ConsoleKey.Delete when Position < Value.Length:
                Value = Value.Remove(Position, NextPosition(Position) - Position);

                return true;
        }

        if (char.IsControl(key.KeyChar))
        {
            return false;
        }

        if (char.IsHighSurrogate(key.KeyChar))
        {
            // ReadKey delivers a supplementary character as two UTF-16 keys.
            _pendingHighSurrogate = key.KeyChar;

            return false;
        }

        var inserted = key.KeyChar.ToString();

        if (char.IsLowSurrogate(key.KeyChar))
        {
            if (pending is null)
            {
                return false;
            }

            inserted = new string(new[] { pending.Value, key.KeyChar });
        }

        Value = Value.Insert(Position, inserted);
        Position += inserted.Length;

        return true;
    }

    private int PreviousPosition(int position)
    {
        if (position <= 0)
        {
            return 0;
        }

        if (position >= 2 && char.IsSurrogatePair(Value, position - 2))
        {
            return position - 2;
        }

        return position - 1;
    }

    private int NextPosition(int position)
    {
        if (position >= Value.Length)
        {
            return Value.Length;
        }

        return position + (char.IsSurrogatePair(Value, position) ? 2 : 1);
    }

    public Measurement Measure(RenderOptions options, int maxWidth)
    {
        return new Measurement(Math.Min(1, maxWidth), maxWidth);
    }

    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        IRenderable markup = new Markup($"[{Theme.Active.Ac}]{Markup.Escape(VisibleText(maxWidth))}[/]");

        return markup.Render(options, maxWidth);
    }

    private string VisibleText(int maxWidth)
    {
        if (maxWidth <= 0)
        {
            return string.Empty;
        }

        var characters = new List<string>();
        var widths = new List<int>();
        var cursorIndex = 0;
        var offset = 0;

        foreach (var rune in Value.EnumerateRunes())
        {
            if (offset < Position)
            {
                cursorIndex++;
            }

            var shown = secret ? "•" : rune.ToString();

            if (!secret && Rune.IsControl(rune))
            {
                shown = "�";
            }

            characters.Add(shown);
            widths.Add(shown.GetCellWidth());
            offset += rune.Utf16SequenceLength;
        }

        if (maxWidth == 1)
        {
            return Cursor;
        }

        if (maxWidth == 2 && widths.Sum() + 1 > maxWidth)
        {
            return cursorIndex > 0 ? $"…{Cursor}" : $"{Cursor}…";
        }

        var start = cursorIndex;
        var end = cursorIndex;
        var contentWidth = 1;
        bool expanded;

        do
        {
            expanded = false;

            if (start > 0 && Fits(start - 1, end, contentWidth + widths[start - 1]))
            {
                contentWidth += widths[--start];
                expanded = true;
            }

            if (end < characters.Count && Fits(start, end + 1, contentWidth + widths[end]))
            {
                contentWidth += widths[end++];
                expanded = true;
            }
        } while (expanded);

        var output = new StringBuilder();

        if (start > 0)
        {
            output.Append('…');
        }

        for (var i = start; i < end; i++)
        {
            if (i == cursorIndex)
            {
                output.Append(Cursor);
            }

            output.Append(characters[i]);
        }

        if (cursorIndex == end)
        {
            output.Append(Cursor);
        }

        if (end < characters.Count)
        {
            output.Append('…');
        }

        return output.ToString();

        bool Fits(int candidateStart, int candidateEnd, int candidateWidth)
        {
            var markers = (candidateStart > 0 ? 1 : 0) + (candidateEnd < characters.Count ? 1 : 0);

            return candidateWidth + markers <= maxWidth;
        }
    }
}
