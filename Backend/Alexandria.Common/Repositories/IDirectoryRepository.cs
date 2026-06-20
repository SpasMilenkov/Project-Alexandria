using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Directories;
using Alexandria.Dto.Files;
using Directory = Alexandria.Data.Models.Directory;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Common.Repositories;

public interface IDirectoryRepository : IRepository<Directory>
{
    Task<Directory?> GetDirectoryMetadataAsync(Guid id, Guid ownerId, CancellationToken ct = default);

    Task<IEnumerable<File>> GetAllFilesInDirectoryAsync(Guid directoryId,
        bool includeSubdirectories = false, CancellationToken ct = default);

    Task<IEnumerable<Directory>> GetUserDirectoriesAsync(Guid ownerId, Guid? parentId = null,
        CancellationToken ct = default);

    Task<List<Directory>> GetSubDirectoriesAsync(Guid directoryId, CancellationToken ct = default);

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
        CancellationToken ct = default);

    Task<List<PathPartDto>> GetDirectoryPathAsync(Guid directoryId, CancellationToken ct = default);
    Task CopyDirectoryAsync(Guid directoryId, Guid? destinationId, Guid userId, CancellationToken ct = default);
    Task MoveDirectoriesAsync(Guid[] ids, Guid? destinationId, Guid userId, CancellationToken ct = default);
    Task<int> RestoreDirectoriesAsync(Guid[] ids, Guid userId, CancellationToken ct = default);
    Task<int> DeleteDirectoryTreeAsync(Guid directoryId, Guid userId, CancellationToken ct = default);

    Task<List<BulkDownloadEntry>> GetBulkDownloadEntriesAsync(
        Guid[] directoryIds,
        Guid[] fileIds,
        Guid userId,
        CancellationToken ct = default);
}