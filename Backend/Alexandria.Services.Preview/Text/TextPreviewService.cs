using System.Text;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;

namespace Alexandria.Services.Preview.Text;

public class TextPreviewService : ITextPreviewService
{
    public const int DefaultMaxBytes = 512 * 1024;
    public const int DefaultMaxLines = 2_000;
    public const int DefaultMaxChars = 200_000;
    public const int DefaultMaxDomElements = 10_000;
    public const int DefaultMaxTokenSpans = DefaultMaxDomElements - DefaultMaxLines;

    /// <summary>
    /// Decodes the head of a file and tokenizes it. Returns null when the content is not text
    /// (binary, or ciphertext of an encrypted file), so callers can skip the preview.
    /// </summary>
    /// <param name="fileStream">Plaintext content. Never pass the ciphertext of an encrypted version.</param>
    /// <param name="fileName">Used to pick the language from the extension or well-known name.</param>
    /// <param name="totalBytes">Full file size when known, shown in the truncation notice.</param>
    /// <param name="maxBytes">Maximum bytes read from the file.</param>
    /// <param name="maxLines">Maximum number of line elements the client may render.</param>
    /// <param name="maxChars">Maximum number of characters sent to the highlighter.</param>
    /// <param name="ct">Cancellation token</param>
    public async Task<TextPreviewDto?> GenerateHighlightedPreviewAsync(
        Stream fileStream,
        string fileName,
        long? totalBytes = null,
        int maxBytes = DefaultMaxBytes,
        int maxLines = DefaultMaxLines,
        int maxChars = DefaultMaxChars,
        CancellationToken ct = default)
    {
        var byteLimit = Math.Clamp(maxBytes, 0, DefaultMaxBytes);
        var charLimit = Math.Clamp(maxChars, 0, DefaultMaxChars);
        var lineLimit = Math.Clamp(maxLines, 0, DefaultMaxLines);

        var (data, bytesTruncated) = await ReadHeadAsync(fileStream, byteLimit, ct);

        if (data.Length == 0)
        {
            var emptyTruncation = bytesTruncated ? TextPreviewTruncation.Bytes : TextPreviewTruncation.None;

            return new TextPreviewDto(
                [],
                PreviewLanguages.Plain.Name,
                bytesTruncated,
                emptyTruncation,
                false,
                0,
                totalBytes ?? (fileStream.CanSeek ? fileStream.Length : (long?)null),
                "utf-8",
                0,
                0,
                0);
        }

        if (!TryDecode(data, bytesTruncated, out var text, out var encodingName, out var encoding)) return null;

        // Normalize before counting lines; otherwise a CR-only file bypasses the line cap and
        // expands into one line per carriage return inside the highlighter.
        text = TextHighlighter.Normalize(text);

        var (limitedText, truncation) = LimitText(text, charLimit, lineLimit, bytesTruncated);

        var firstLineEnd = limitedText.IndexOf('\n');
        var firstLine = firstLineEnd < 0 ? limitedText : limitedText[..firstLineEnd];
        var language = PreviewLanguages.FromFileName(fileName, firstLine);

        var tokenLimit = Math.Min(DefaultMaxTokenSpans, Math.Max(0, DefaultMaxDomElements - lineLimit));
        var result = TextHighlighter.Highlight(limitedText, language, tokenLimit);

        return new TextPreviewDto(
            result.Lines,
            language.Name,
            truncation != TextPreviewTruncation.None,
            truncation,
            result.TokensLimited,
            encoding.GetByteCount(limitedText),
            totalBytes ?? (fileStream.CanSeek ? fileStream.Length : (long?)null),
            encodingName,
            result.LineCount,
            result.TokenCount,
            result.DomElementCount);
    }

    /// <summary>
    /// Applies the content caps before highlighting. Line and character caps are separate: the
    /// line cap bounds the number of line elements, while the character cap bounds one long line.
    /// </summary>
    private static (string Text, TextPreviewTruncation Truncation) LimitText(
        string text,
        int maxChars,
        int maxLines,
        bool bytesTruncated)
    {
        var truncation = bytesTruncated ? TextPreviewTruncation.Bytes : TextPreviewTruncation.None;

        if (bytesTruncated)
        {
            // Keep the byte cut on a whole line when one is available near the end.
            var lastNewline = text.LastIndexOf('\n');
            if (lastNewline > 0 && lastNewline >= text.Length / 2) text = text[..(lastNewline + 1)];
        }

        var charLimit = Math.Max(0, maxChars);
        if (text.Length > charLimit)
        {
            text = text[..CutAtCharacterLimit(text, charLimit)];
            if (truncation == TextPreviewTruncation.None) truncation = TextPreviewTruncation.Characters;
        }

        if (maxLines >= 0)
        {
            var cut = LineLimitCut(text, maxLines);
            if (cut < text.Length)
            {
                text = text[..cut];
                if (truncation == TextPreviewTruncation.None || truncation == TextPreviewTruncation.Characters)
                    truncation = TextPreviewTruncation.Lines;
            }
        }

        return (text, truncation);
    }

    private static int CutAtCharacterLimit(string text, int maxChars)
    {
        if (maxChars <= 0) return 0;
        if (text.Length <= maxChars) return text.Length;

        // Prefer a whole line when the cut falls near one, but never exceed the cap.
        var lastNewline = text.LastIndexOf('\n', maxChars - 1, maxChars);
        var cut = lastNewline > 0 && lastNewline >= maxChars / 2 ? lastNewline + 1 : maxChars;

        // Do not split a surrogate pair at the character cap.
        if (cut < text.Length && char.IsHighSurrogate(text[cut - 1]) && char.IsLowSurrogate(text[cut])) cut--;
        return cut;
    }

    private static int LineLimitCut(string text, int maxLines)
    {
        if (maxLines <= 0) return 0;

        var newlines = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;

            newlines++;
            if (newlines == maxLines)
                return i + 1 < text.Length ? i + 1 : text.Length;
        }

        return text.Length;
    }

    /// <summary>Reads up to maxBytes, plus one probe byte to tell "exactly maxBytes" from "more".</summary>
    private static async Task<(byte[] Data, bool Truncated)> ReadHeadAsync(
        Stream stream, int maxBytes, CancellationToken ct)
    {
        var buffer = new byte[maxBytes + 1];
        var total = 0;

        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), ct);
            if (read == 0) break;
            total += read;
        }

        return (buffer.AsSpan(0, Math.Min(total, maxBytes)).ToArray(), total > maxBytes);
    }

    /// <summary>
    /// Decodes BOM-marked UTF-8 and UTF-16, then strict UTF-8, then Latin-1. A Decoder is used so a
    /// character cut by the byte limit is held back instead of becoming U+FFFD. Returns false for
    /// content that is not text.
    /// </summary>
    private static bool TryDecode(
        byte[] data, bool truncated, out string text, out string encodingName, out Encoding encoding)
    {
        text = string.Empty;
        encodingName = "utf-8";
        encoding = new UTF8Encoding(false, true);

        var offset = 0;
        var utf16 = false;

        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
        {
            offset = 3;
        }
        else if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
        {
            offset = 2;
            utf16 = true;
            encodingName = "utf-16le";
            encoding = new UnicodeEncoding(false, false, true);
        }
        else if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
        {
            offset = 2;
            utf16 = true;
            encodingName = "utf-16be";
            encoding = new UnicodeEncoding(true, false, true);
        }
        else if (Array.IndexOf(data, (byte)0) >= 0)
        {
            return false;
        }

        try
        {
            text = Decode(encoding, data, offset, flush: !truncated);
        }
        catch (DecoderFallbackException)
        {
            if (utf16) return false;

            encodingName = "latin1";
            encoding = Encoding.Latin1;
            text = Decode(encoding, data, offset, flush: true);
        }

        return !LooksBinary(text);
    }

    private static string Decode(Encoding encoding, byte[] data, int offset, bool flush)
    {
        var decoder = encoding.GetDecoder();
        var chars = new char[encoding.GetMaxCharCount(data.Length - offset)];
        var count = decoder.GetChars(data, offset, data.Length - offset, chars, 0, flush);
        return new string(chars, 0, count);
    }

    /// <summary>More than 1% control characters means this is not text worth previewing.</summary>
    private static bool LooksBinary(string text)
    {
        var controls = 0;
        foreach (var c in text)
            if ((c < ' ' && c != '\t' && c != '\n' && c != '\r' && c != '\f') || c == '\u007f')
                controls++;

        return controls > 0 && controls * 100L > text.Length;
    }
}
