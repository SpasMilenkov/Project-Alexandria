namespace Alexandria.Dto.Files;

public enum TextPreviewTruncation
{
    None = 0,
    Bytes = 1,
    Characters = 2,
    Lines = 3
}

/// <summary>
/// A token range within a <see cref="TextPreviewLineDto.Text"/>. Start and length are UTF-16
/// code units, matching JavaScript string offsets.
/// </summary>
public sealed record TextPreviewTokenDto(int Start, int Length, string Kind);

/// <summary>
/// One preview line. Text includes the trailing newline; token ranges never include it.
/// </summary>
public sealed record TextPreviewLineDto(string Text, IReadOnlyList<TextPreviewTokenDto> Tokens);

/// <summary>
/// A tokenized text preview. The client renders line text and token ranges as text nodes and
/// spans, so no markup is produced or stored by the server.
/// <para>
/// <see cref="DomElementCount"/> counts line spans plus token spans, excluding preview chrome.
/// </para>
/// </summary>
public sealed record TextPreviewDto(
    IReadOnlyList<TextPreviewLineDto> Lines,
    string Language,
    bool Truncated,
    TextPreviewTruncation Truncation,
    bool HighlightingLimited,
    long BytesShown,
    long? TotalBytes,
    string Encoding,
    int LineCount,
    int TokenCount,
    int DomElementCount);
