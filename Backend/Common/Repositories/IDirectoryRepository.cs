using DTO.Directories;
using DTO.Files;
using Models.Enumerators;
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
    
    Task<List<Directory>> GetSubDirectories(Guid directoryId, CancellationToken ct);

    Task<PaginatedResult<DirectorySummaryDto>> GetRootDirectoriesAsync(
        Guid ownerId,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default);
    
    Task<PaginatedResult<DirectorySummaryDto>> GetSubdirectoriesAsync(Guid parentDirectoryId, Guid userId,
        int currentPage = 1,
        int pageSize = 25,
        SortDirection sortDirection = SortDirection.Asc,
        DirectorySortBy sortBy = DirectorySortBy.Name,
        CancellationToken ct = default);

    Task<PaginatedResult<FileSummary>> GetRootFilesAsync(
        Guid ownerId,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default);
    
    Task<PaginatedResult<DirectorySummaryDto>> FindDirectoryAsync(Guid userId, DirectorySearchQuery query, CancellationToken ct);
    Task<List<PathPartDto>> GetDirectoryPathAsync(Guid directoryId, CancellationToken ct = default);
}