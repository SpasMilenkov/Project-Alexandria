namespace Common.Services;

public interface ITextPreviewService
{
    Task<(string data, string mimeType)> GenerateTextPreviewAsync(Stream fileStream,
        string mimeType,
        int maxBytes = 524288, // 512KB preview limit
        CancellationToken ct = default
    );
}