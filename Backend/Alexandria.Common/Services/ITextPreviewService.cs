using Alexandria.Dto.Files;

namespace Alexandria.Common.Services;

public interface ITextPreviewService
{
    /// <summary>
    /// Decodes and tokenizes the head of a text file. Returns null for binary content.
    /// </summary>
    Task<TextPreviewDto?> GenerateHighlightedPreviewAsync(
        Stream fileStream,
        string fileName,
        long? totalBytes = null,
        int maxBytes = 524288,
        int maxLines = 2_000,
        int maxChars = 200_000,
        CancellationToken ct = default);
}
