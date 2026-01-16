namespace Common.Services;

public interface IArchivePreviewService
{
    Task<(string? data, string mimeType)> GenerateArchivePreviewAsync(Stream archiveStream,
        string fileName,
        CancellationToken ct);
}