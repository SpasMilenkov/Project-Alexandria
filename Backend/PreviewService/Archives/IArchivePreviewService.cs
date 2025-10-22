namespace PreviewService.Archives;

public interface IArchivePreviewService
{
    Task<(byte[] data, string mimeType)> GenerateArchivePreviewAsync(
        Stream archiveStream,
        string fileName,
        CancellationToken ct);
}