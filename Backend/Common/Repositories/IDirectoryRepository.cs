using DTO.Directories;
using Directory = Models.Directory;
using File = Models.File;

namespace Common.Repositories;

public interface IDirectoryRepository: IRepository<Directory>
{
    Task<Directory?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<Directory?> GetDirectoryMetadataAsync(Guid id, Guid ownerId, CancellationToken ct = default);
    Task<IEnumerable<File>> GetAllFilesInDirectoryAsync(Guid directoryId,
        bool includeSubdirectories = false, CancellationToken ct = default);

    Task<IEnumerable<Directory>> GetUserDirectories( Guid ownerId, Guid? parentId = null,
        CancellationToken ct = default);

    Task<RootContentSummaryDto> GetRootContentSummaryAsync(Guid ownerId, CancellationToken ct = default);
    Task<List<Directory>> GetSubDirectories(Guid directoryId, CancellationToken ct);
    Task<RootContent> GetRootContentAsync(Guid ownerId, CancellationToken ct = default);

    // Task<IEnumerable<Directory>> GetRootDirectoriesAsync(Guid ownerId, CancellationToken ct = default);

    Task<string> GetDirectoryPathAsync(Guid directoryId, CancellationToken ct = default);
}