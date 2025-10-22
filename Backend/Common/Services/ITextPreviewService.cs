namespace Common.Services;

public interface ITextPreviewService
{
    Task<(byte[] data, string mimeType)> GenerateTextPreviewAsync(
        Stream fileStream,
        string mimeType,
        int maxBytes = 512 * 1024, // 512KB preview limit
        CancellationToken ct = default);
}