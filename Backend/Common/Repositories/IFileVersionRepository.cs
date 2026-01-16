using Models;

namespace Common.Repositories;

public interface IFileVersionRepository : IRepository<FileVersion>
{
    Task<int> DeleteAllVersionsOfAFile(Guid fileId, CancellationToken ct = default);
    Task<int> DeleteFileVersions(Guid[] fileIds, Guid ownerId, CancellationToken ct = default);
}