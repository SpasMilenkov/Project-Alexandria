using System.Text;
using System.Text.Json;
using SharpCompress.Archives;

namespace PreviewService.Archives;

public class ArchivePreviewService : IArchivePreviewService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<(byte[] data, string mimeType)> GenerateArchivePreviewAsync(
        Stream archiveStream,
        string fileName,
        CancellationToken ct)
    {
        try
        {
            using var archive = ArchiveFactory.Open(archiveStream);

            var entries = archive.Entries
                .Where(e => !e.IsDirectory)
                .Take(200) // limit to first 200 files to prevent large output
                .Select(e => new
                {
                    e.Key,
                    SizeKB = e.Size / 1024,
                    Modified = e.LastModifiedTime
                })
                .ToList();

            var previewData = new
            {
                FileCount = archive.Entries.Count(),
                FileName = fileName,
                Entries = entries
            };

            var json = JsonSerializer.Serialize(previewData, JsonOptions);
            return (Encoding.UTF8.GetBytes(json), "application/json");
        }
        catch (Exception ex)
        {
            // Graceful fallback — return a short JSON error message
            var error = JsonSerializer.Serialize(new
            {
                Error = "Unable to preview archive.",
                Message = ex.Message,
                FileName = fileName
            }, JsonOptions);

            return (Encoding.UTF8.GetBytes(error), "application/json");
        }
    }
}
