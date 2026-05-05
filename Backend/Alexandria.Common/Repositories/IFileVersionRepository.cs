using Alexandria.Data.Models;
using Alexandria.Dto.Files;

namespace Alexandria.Common.Repositories;

public interface IFileVersionRepository : IRepository<FileVersion>
{
    Task<int> DeleteFileVersionsAsync(Guid[] fileIds, Guid ownerId, CancellationToken ct = default);
    Task<int> SoftDeleteFileVersionsAsync(Guid[] fileIds, Guid ownerId, CancellationToken ct = default);
    Task<int> SoftDeleteFileVersionsAsync(Guid fileId, Guid ownerId, CancellationToken ct = default);
    Task<int> RestoreFileVersionsAsync(Guid[] fileIds, Guid ownerId, CancellationToken ct = default);
    Task<DownloadMetadata?> GetDownloadMetadataAsync(Guid versionId, Guid userId, CancellationToken ct = default);

    Task<PaginatedResult<FileVersionDto>> GetVersionsForFileAsync(Guid fileId, Guid ownerId, int page = 1,
        int pageSize = 10,
        CancellationToken ct = default);

    Task<bool> IsPromotedAsync(Guid versionId, CancellationToken ct = default);
    Task<bool> IsEncryptedAsync(Guid versionId, CancellationToken ct = default);
    Task<FileVersionDto?> GetMostRecentAsync(Guid fileId, Guid userId, CancellationToken ct = default);
    Task RestoreFileVersionAsync(Guid versionId, Guid userId, CancellationToken ct = default);
    Task RemoveAsync(Guid versionId, Guid ownerId, CancellationToken ct = default);
    Task<VersionDownloadInfo?> GetVersionDownloadInfoAsync(Guid versionId, Guid userId, CancellationToken ct = default);
}