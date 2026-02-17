using Models;

namespace Common.Repositories;

public interface IFileVersionRepository : IRepository<FileVersion>
{
    Task<int> DeleteFileVersions(Guid[] fileIds, Guid ownerId, CancellationToken ct = default);
    Task<int> SoftDeleteFileVersions(Guid[] fileIds, Guid ownerId, CancellationToken ct = default);
    Task<int> RestoreFileVersions(Guid[] fileIds, Guid ownerId, CancellationToken ct = default);
}
