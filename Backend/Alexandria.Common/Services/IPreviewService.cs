using Alexandria.Dto.Files;

namespace Alexandria.Common.Services;

public interface IPreviewService
{
    Task<PreviewResultDto?> GetPreviewUrlAsync(Guid fileId, Guid ownerId, CancellationToken ct);
    // public Task<FileResultSummary?> GetThumbnail(Guid fileId, int width, int height, CancellationToken ct = default);
}