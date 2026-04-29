using System.Text.Json;
using Alexandria.Common.Services;
using SharpCompress.Archives;

namespace Alexandria.Services.Preview.Archives;

public class ArchivePreviewService : IArchivePreviewService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<(string? data, string mimeType)> GenerateArchivePreviewAsync(
        Stream archiveStream,
        string fileName,
        CancellationToken ct = default)
    {
        try
        {
            var archive = await ArchiveFactory.OpenAsyncArchive(archiveStream, cancellationToken: ct);

            var entries = await archive.EntriesAsync
                .Where(e => !e.IsDirectory)
                .Take(200) // limit to first 200 files to prevent large output
                .Select(e => new
                {
                    e.Key,
                    SizeKB = e.Size / 1024,
                    Modified = e.LastModifiedTime
                })
                .ToListAsync(ct);

            var previewData = new
            {
                FileCount = entries.Count(),
                FileName = fileName,
                Entries = entries
            };

            var json = JsonSerializer.Serialize(previewData, JsonOptions);
            return (json, "application/json");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Could not generate archive preview", ex);
            throw;
        }
    }
}