using DTO.Files;

namespace Common.Services;

public interface IPreviewService
{
    Task<PreviewResultDto?> GetPreviewUrl(Guid fileId, Guid ownerId, CancellationToken ct);
    // public Task<FileResultSummary?> GetPreview(Guid fileId, CancellationToken ct);
    //
    // public Task<FileResultSummary?> GetThumbnail(Guid fileId, int width, int height, CancellationToken ct = default);
}