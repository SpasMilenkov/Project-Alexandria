using DTO.Directories;
using Directory = Models.Directory;
using File = Models.File;
namespace Common.Services;

public interface IDirectoryService
{
    Task<DirectorySummaryDto> CreateDirectoryAsync(string name, Guid ownerId, Guid? parentId = null, 
        CancellationToken ct = default);
    
    Task<Directory> GetDirectoryByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<DirectorySummaryDto> GetDirectoryDtoByIdAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task<DirectoryDto> GetDirectoryWithDetailsAsync(Guid id, Guid userId, CancellationToken ct = default);
    
    Task<IEnumerable<DirectorySummaryDto>> GetUserDirectoriesAsync(Guid userId, Guid? parentId = null, 
        CancellationToken ct = default);
    
    Task<RootContentSummaryDto> GetRootDirectoriesAsync(Guid userId, CancellationToken ct = default);
    
    Task<IEnumerable<DirectorySummaryDto>> GetSubDirectoriesAsync(Guid directoryId, Guid userId,
        CancellationToken ct = default);
    
    Task<IEnumerable<File>> GetDirectoryFilesAsync(Guid directoryId, Guid userId, 
        bool includeSubdirectories = false, CancellationToken ct = default);
    
    Task<string> GetDirectoryPathAsync(Guid directoryId, Guid userId, CancellationToken ct = default);
    
    Task<DirectoryDto> UpdateDirectoryAsync(Guid id, string name, Guid userId, 
        CancellationToken ct = default);
    
    Task<DirectorySummaryDto> MoveDirectoryAsync(Guid id, Guid? newParentId, Guid userId,
        CancellationToken ct = default);
    
    Task DeleteDirectoryAsync(Guid id, Guid userId, bool force = false, 
        CancellationToken ct = default);
    
    Task<bool> DirectoryExistsAsync(Guid id, CancellationToken ct = default);
    
    Task<bool> HasAccessToDirectoryAsync(Guid directoryId, Guid userId, CancellationToken ct = default);
}