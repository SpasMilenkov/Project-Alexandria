using System.Text;
using Alexandria.Common.Services;

namespace Alexandria.Services.Preview.Documents.BodyText;

/// <summary>
/// Gathers a document's text while normalizing it, so no reader ever holds more than the stored
/// limit. Runs of whitespace collapse to one space or one line break, control characters are
/// dropped (Postgres rejects the null character outright), and every page break is kept as a
/// form feed so the n-th segment stays the n-th page.
/// </summary>
internal sealed class TextCollector(int maxCharacters = DocumentTextExtractor.MaxStoredCharacters)
{
    private const char PageSeparator = '\f';

    private readonly StringBuilder _text = new();
    private TextSeparator _pending = TextSeparator.None;

    /// <summary>True once the limit is reached; readers stop as soon as they see it.</summary>
    public bool IsFull { get; private set; }

    /// <summary>True when part of the document was left out, for any reason.</summary>
    public bool IsTruncated { get; private set; }

    /// <summary>Characters other than whitespace, which tells a scan from a real text layer.</summary>
    public int VisibleCharacters { get; private set; }

    /// <param name="text">The text to add.</param>
    /// <param name="inline">True for markup text (XML, HTML), where a newline in the source is only
    /// formatting and breaks come from the elements instead.</param>
    public void Append(string? text, bool inline = false)
    {
        if (text is null)
            return;

        Append(text.AsSpan(), inline);
    }

    /// <inheritdoc cref="Append(string?, bool)"/>
    public void Append(ReadOnlySpan<char> text, bool inline = false)
    {
        var i = 0;

        while (i < text.Length && !IsFull)
            i += AppendNext(text, i, inline);
    }

    // Handles the character at the index and returns how many characters it consumed.
    private int AppendNext(ReadOnlySpan<char> text, int index, bool inline)
    {
        var c = text[index];

        if (inline && c is PageSeparator or '\n' or '\r')
        {
            Space();
            return 1;
        }

        if (c == PageSeparator)
        {
            PageBreak();
            return 1;
        }

        if (c is '\n' or '\r')
        {
            LineBreak();
            return 1;
        }

        if (char.IsWhiteSpace(c))
        {
            Space();
            return 1;
        }

        // Invalid bytes decode to the replacement character, which only adds noise.
        if (char.IsControl(c) || c == '�')
            return 1;

        if (char.IsHighSurrogate(c))
        {
            var isPair = index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]);
            if (!isPair)
                return 1;

            AppendVisible(text.Slice(index, 2));
            return 2;
        }

        // A low surrogate on its own is a broken pair and would fail UTF-8 encoding.
        if (char.IsLowSurrogate(c))
            return 1;

        AppendVisible(text.Slice(index, 1));
        return 1;
    }

    public void Space() => Raise(TextSeparator.Space);

    public void LineBreak() => Raise(TextSeparator.Line);

    /// <summary>
    /// Writes a form feed right away, even after an empty page, so page numbers stay aligned.
    /// Pending spaces and line breaks before it are dropped.
    /// </summary>
    public void PageBreak()
    {
        if (IsFull)
            return;

        _pending = TextSeparator.None;
        Write(PageSeparator.ToString());
    }

    /// <summary>Records that some of the document was skipped, for example an oversized part.</summary>
    public void MarkTruncated() => IsTruncated = true;

    public string Build() => _text.ToString().TrimEnd(' ', '\n', PageSeparator);

    private void Raise(TextSeparator separator)
    {
        if (separator > _pending)
            _pending = separator;
    }

    private void AppendVisible(ReadOnlySpan<char> chars)
    {
        var separator = SeparatorText();
        _pending = TextSeparator.None;

        if (!Fits(separator.Length + chars.Length))
            return;

        _text.Append(separator);
        _text.Append(chars);
        VisibleCharacters += chars.Length;
    }

    // Separators only go between text, so a page or paragraph never starts with one.
    private string SeparatorText()
    {
        if (_text.Length == 0 || _text[^1] == PageSeparator)
            return string.Empty;

        if (_pending == TextSeparator.Line)
            return "\n";

        if (_pending == TextSeparator.Space)
            return " ";

        return string.Empty;
    }

    private void Write(string value)
    {
        if (Fits(value.Length))
            _text.Append(value);
    }

    private bool Fits(int length)
    {
        if (_text.Length + length <= maxCharacters)
            return true;

        IsFull = true;
        IsTruncated = true;
        return false;
    }
}