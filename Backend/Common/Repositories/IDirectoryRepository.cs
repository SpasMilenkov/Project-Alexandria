using DTO.Directories;
using DTO.Files;
using Models.Enumerators;
using Directory = Models.Directory;
using File = Models.File;

namespace Common.Repositories;

public interface IDirectoryRepository : IRepository<Directory>
{
    Task<Directory?> GetDirectoryMetadataAsync(Guid id, Guid ownerId, CancellationToken ct = default);

    Task<IEnumerable<File>> GetAllFilesInDirectoryAsync(Guid directoryId,
        bool includeSubdirectories = false, CancellationToken ct = default);

    Task<IEnumerable<Directory>> GetUserDirectories(Guid ownerId, Guid? parentId = null,
        CancellationToken ct = default);

    Task<List<Directory>> GetSubDirectories(Guid directoryId, CancellationToken ct);

    Task<PaginatedResult<DirectorySummaryDto>> GetRootDirectoriesAsync(
        Guid ownerId,
        int page = 1,
        int pageSize = 25,
        SortBy sortBy = SortBy.Name,
        SortDirection sortDirection = SortDirection.Asc,
        CancellationToken ct = default);

    Task<PaginatedResult<DirectorySummaryDto>> GetSubdirectoriesAsync(Guid parentDirectoryId, Guid userId,
        int currentPage = 1,
        int pageSize = 25,
        SortDirection sortDirection = SortDirection.Asc,
        SortBy sortBy = SortBy.Name,
        CancellationToken ct = default);

    Task<PaginatedResult<FileResult>> GetRootFilesAsync(
        Guid ownerId,
        int page = 1,
        int pageSize = 25,
        SortBy sortBy = SortBy.Name,
        SortDirection sortDirection = SortDirection.Asc,
        CancellationToken ct = default);

    Task<PaginatedResult<DirectorySummaryDto>> FindDirectoryAsync(Guid userId, DirectorySearchQuery query,
        CancellationToken ct);

    Task<List<PathPartDto>> GetDirectoryPathAsync(Guid directoryId, CancellationToken ct = default);
    Task CopyDirectory(Guid directoryId, Guid? destinationId, Guid userId, CancellationToken ct = default);
    Task MoveDirectories(Guid[] ids, Guid? destinationId, Guid userId, CancellationToken ct = default);
    Task<int> RestoreDirectories(Guid[] ids, Guid userId, CancellationToken ct = default);
}