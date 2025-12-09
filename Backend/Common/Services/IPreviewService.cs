using DTO;
using DTO.Files;

namespace Common.Services;

public interface IPreviewService
{
    public Task<FileResultSummary?> GetPreview(Guid fileId, CancellationToken ct);

    public Task<FileResultSummary?> GetThumbnail(Guid fileId, int width, int height, CancellationToken ct = default);
}