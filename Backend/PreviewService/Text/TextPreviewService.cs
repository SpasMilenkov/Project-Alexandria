using System.Text;
using Common.Services;

namespace PreviewService.Text;

public class TextPreviewService : ITextPreviewService
{
    public async Task<(string data, string mimeType)> GenerateTextPreviewAsync(
        Stream fileStream,
        string mimeType,
        int maxBytes = 512 * 1024, // 512KB preview limit
        CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        var totalRead = 0;

        while (totalRead < maxBytes)
        {
            var toRead = Math.Min(buffer.Length, maxBytes - totalRead);
            var bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, toRead), ct);
            if (bytesRead == 0)
                break;

            await ms.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalRead += bytesRead;
        }

        // Ensure UTF-8 integrity (cut safely)
        var data = ms.ToArray();
        var safeLength = data.Length;
        while (safeLength > 0 && (data[safeLength - 1] & 0b11000000) == 0b10000000)
            safeLength--; // drop dangling UTF-8 continuation bytes

        var text = Encoding.UTF8.GetString(data, 0, safeLength);
        if (totalRead >= maxBytes)
            text += "\n\n... (preview truncated)";

        return (text, mimeType);
    }
}