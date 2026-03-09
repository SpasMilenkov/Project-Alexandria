using DTO.Files;
using Models;

namespace Common.Repositories;

public interface IFileVersionRepository : IRepository<FileVersion>
{
    Task<int> DeleteFileVersions(Guid[] fileIds, Guid ownerId, CancellationToken ct = default);
    Task<int> SoftDeleteFileVersions(Guid[] fileIds, Guid ownerId, CancellationToken ct = default);
    Task<int> SoftDeleteFileVersions(Guid fileId, Guid ownerId, CancellationToken ct = default);
    Task<int> RestoreFileVersions(Guid[] fileIds, Guid ownerId, CancellationToken ct = default);

    Task<PaginatedResult<FileVersionDto>> GetVersionsForFile(Guid fileId, Guid ownerId, int page = 1, int pageSize = 10,
        CancellationToken ct = default);

    Task<byte[]?> GetContentHashByVersionId(Guid versionId, Guid userId, CancellationToken ct = default);
    Task<FileVersionDto?> GetMostRecent(Guid fileId, Guid userId, CancellationToken ct = default);
    Task RestoreFileVersion(Guid versionId, Guid userId, CancellationToken ct = default);
    Task RemoveAsync(Guid versionId, Guid ownerId, CancellationToken ct = default);
    Task<VersionDownloadInfo?> GetVersionDownloadInfo(Guid versionId, Guid userId, CancellationToken ct = default);
}