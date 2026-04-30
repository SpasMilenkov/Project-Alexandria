using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Directories;
using Alexandria.Dto.Files;
using Directory = Alexandria.Data.Models.Directory;

namespace Alexandria.Common.Services;

public interface IDirectoryService
{
    Task<DirectorySummaryDto> CreateDirectoryAsync(string name, Guid ownerId, Guid? parentId = null,
        CancellationToken ct = default);

    Task<Dictionary<string, Guid?>> CreateDirectorySubTreeAsync(List<string> paths, Guid? parentId, Guid userId,
        CancellationToken ct = default);

    Task<Directory> GetDirectoryByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<DirectorySummaryDto> GetDirectoryDtoByIdAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task<PaginatedResult<DirectorySummaryDto>> FindDirectoryAsync(Guid userId, DirectorySearchQuery query,
        CancellationToken ct = default);

    Task<PaginatedResult<DirectorySummaryDto>> GetPaginatedDirectoriesAsync(Guid id, Guid userId,
        int currentPage = 1,
        int pageSize = 25,
        SortDirection sortDirection = SortDirection.Asc,
        SortBy sortBy = SortBy.Name,
        CancellationToken ct = default);

    Task<PaginatedResult<DirectorySummaryDto>> GetRootDirectoriesAsync(Guid ownerId,
        int page = 1,
        int pageSize = 25,
        SortDirection sortDirection = SortDirection.Asc,
        SortBy sortBy = SortBy.Name,
        CancellationToken ct = default);

    Task<List<PathPartDto>> GetDirectoryPathAsync(Guid directoryId, Guid userId, CancellationToken ct = default);

    Task<DirectoryDto> UpdateDirectoryAsync(Guid id, string name, Guid userId,
        CancellationToken ct = default);

    Task MoveDirectoryAsync(Guid[] ids, Guid? newParentId, Guid userId,
        CancellationToken ct = default);

    Task DeleteDirectoryAsync(Guid id, Guid userId, bool force = false,
        CancellationToken ct = default);

    Task<bool> DirectoryExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> DirectoryExistsWithOwnershipAsync(Guid id, Guid ownerId, CancellationToken ct = default);
    Task<bool> HasAccessToDirectoryAsync(Guid directoryId, Guid userId, CancellationToken ct = default);

    Task CopyDirectoryAsync(Guid directoryId, Guid? destinationId, Guid userId,
        CancellationToken ct = default);

    Task<int> RestoreDirectories(Guid[] directoryIds, Guid userId, CancellationToken ct = default);
}