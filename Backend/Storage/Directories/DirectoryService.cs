using Common;
using Common.Services;
using DTO.Directories;
using DTO.Files;
using Models.Enumerators;
using Storage.Directories.Exceptions;
using DirectoryNotFoundException = Storage.Directories.Exceptions.DirectoryNotFoundException;
using File = Models.File;

namespace Storage.Directories;

using Directory = Models.Directory;

public class DirectoryService : IDirectoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private static readonly char[] InvalidChars = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    public DirectoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DirectorySummaryDto> CreateDirectoryAsync(string name, Guid ownerId, Guid? parentId = null, 
        CancellationToken ct = default)
    {
        ValidateDirectoryName(name);

        // Check if directory with same name exists in same location
        var existingDirectory = await _unitOfWork.Directories.FirstOrDefaultAsync(
            d => d.Name == name && d.ParentId == parentId && d.OwnerId == ownerId && d.DeletedAt == null, ct);

        if (existingDirectory != null)
        {
            throw new DirectoryAlreadyExistsException(name, parentId);
        }

        // Verify parent directory exists and user has access
        if (parentId.HasValue)
        {
            var parentDirectory = await _unitOfWork.Directories.GetByIdAsync(parentId.Value, ct);
            if (parentDirectory == null)
            {
                throw new DirectoryNotFoundException(parentId.Value);
            }

            if (parentDirectory.OwnerId != ownerId)
            {
                throw new UnauthorizedDirectoryAccessException(parentId.Value, ownerId);
            }
        }

        var directory = new Directory
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwnerId = ownerId,
            ParentId = parentId,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.Directories.AddAsync(directory, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            
            return directory.ToSummaryDto();
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Directory> GetDirectoryByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var directory = await _unitOfWork.Directories.GetByIdAsync(id, ct);
        
        if (directory == null)
        {
            throw new DirectoryNotFoundException(id);
        }

        if (directory.OwnerId != userId)
        {
            throw new UnauthorizedDirectoryAccessException(id, userId);
        }

        return directory;
    }

    public async Task<DirectorySummaryDto> GetDirectoryDtoByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var dir = await _unitOfWork.Directories.GetDirectoryMetadataAsync(id, userId, ct);
        
        if (dir is null) throw new DirectoryNotFoundException(id);
        
        return dir.ToSummaryDto();
    }

    public Task<PaginatedResult<DirectorySummaryDto>> FindDirectoryAsync(Guid userId, DirectorySearchQuery query, CancellationToken ct = default)
    {
        return _unitOfWork.Directories.FindDirectoryAsync(userId, query, ct);
    }

    public async Task<DirectoryDto> GetDirectoryWithDetailsAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var directory = await _unitOfWork.Directories.GetByIdWithDetailsAsync(id, ct);
        
        if (directory == null)
        {
            throw new DirectoryNotFoundException(id);
        }

        if (directory.OwnerId != userId)
        {
            throw new UnauthorizedDirectoryAccessException(id, userId);
        }

        return directory.ToDto();
    }

    public async Task<PaginatedResult<DirectorySummaryDto>> GetPaginatedDirectories(Guid id, Guid userId,
        int currentPage = 1,
        int pageSize = 25,
        SortDirection sortDirection = SortDirection.Asc,
        DirectorySortBy sortBy = DirectorySortBy.Name,
        CancellationToken ct = default)
    {
        return await _unitOfWork.Directories.GetSubdirectoriesAsync(id, userId, currentPage, pageSize, sortDirection, sortBy, ct );
    }

    public async Task<PaginatedResult<DirectorySummaryDto>> GetRootDirectoriesAsync(Guid ownerId, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        return await _unitOfWork.Directories.GetRootDirectoriesAsync(ownerId, page, pageSize, ct);
    }


    public async Task<IEnumerable<DirectorySummaryDto>> GetUserDirectoriesAsync(Guid userId, Guid? parentId = null, 
        CancellationToken ct = default)
    {
        var dirs = await _unitOfWork.Directories.GetUserDirectories(userId, parentId, ct);
        return dirs.Select(d => d.ToSummaryDto());
    }

    /*
     * TODO: There should be a way to make this smarter with the ownership and getting the children
     * too tired to do it right now
     */
    public async Task<IEnumerable<DirectorySummaryDto>> GetSubDirectoriesAsync(Guid directoryId, Guid userId,
        CancellationToken ct = default)
    {
        await VerifyDirectoryAccessAsync(directoryId, userId, ct);
        var dirs = await _unitOfWork.Directories.GetSubDirectories(directoryId, ct);

        return dirs.Select(d => d.ToSummaryDto());
    }

    public async Task<IEnumerable<File>> GetDirectoryFilesAsync(Guid directoryId, Guid userId, 
        bool includeSubdirectories = false, CancellationToken ct = default)
    {
        await VerifyDirectoryAccessAsync(directoryId, userId, ct);
        
        return await _unitOfWork.Directories.GetAllFilesInDirectoryAsync(directoryId, includeSubdirectories, ct);
    }

    public async Task<List<PathPartDto>> GetDirectoryPathAsync(Guid directoryId, Guid userId,
        CancellationToken ct = default)
    {
        await VerifyDirectoryAccessAsync(directoryId, userId, ct);
        
        return await _unitOfWork.Directories.GetDirectoryPathAsync(directoryId, ct);
    }

    public async Task<DirectoryDto> UpdateDirectoryAsync(Guid id, string name, Guid userId, 
        CancellationToken ct = default)
    {
        ValidateDirectoryName(name);

        var directory = await GetDirectoryByIdAsync(id, userId, ct);

        // Check if another directory with same name exists in same location
        var existingDirectory = await _unitOfWork.Directories.FirstOrDefaultAsync(
            d => d.Name == name && d.ParentId == directory.ParentId && 
                 d.OwnerId == userId && d.Id != id && d.DeletedAt == null, ct);

        if (existingDirectory != null)
        {
            throw new DirectoryAlreadyExistsException(name, directory.ParentId);
        }

        directory.Name = name;
        directory.UpdatedAt = DateTime.UtcNow;
        directory.UpdatedBy = userId;

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            _unitOfWork.Directories.Update(directory);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            
            return directory.ToDto();
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<DirectorySummaryDto> MoveDirectoryAsync(Guid id, Guid? newParentId, Guid userId, 
        CancellationToken ct = default)
    {
        var directory = await GetDirectoryByIdAsync(id, userId, ct);

        // Verify new parent directory exists and is accessible
        if (newParentId.HasValue)
        {
            var newParent = await _unitOfWork.Directories.GetByIdAsync(newParentId.Value, ct);
            if (newParent == null)
            {
                throw new DirectoryNotFoundException(newParentId.Value);
            }

            if (newParent.OwnerId != userId)
            {
                throw new UnauthorizedDirectoryAccessException(newParentId.Value, userId);
            }

            // Check for circular reference
            if (await WouldCreateCircularReferenceAsync(id, newParentId.Value, ct))
            {
                throw new CircularDirectoryReferenceException(id, newParentId.Value);
            }
        }

        // Check if directory with same name exists in destination
        var existingDirectory = await _unitOfWork.Directories.FirstOrDefaultAsync(
            d => d.Name == directory.Name && d.ParentId == newParentId && 
                 d.OwnerId == userId && d.Id != id && d.DeletedAt == null, ct);

        if (existingDirectory != null)
        {
            throw new DirectoryAlreadyExistsException(directory.Name, newParentId);
        }

        directory.ParentId = newParentId;
        directory.UpdatedAt = DateTime.UtcNow;
        directory.UpdatedBy = userId;

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            _unitOfWork.Directories.Update(directory);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            
            return directory.ToSummaryDto();
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    ///     Deletes a directory from the system using recursive call
    /// </summary>
    /// <param name="id">Id of the directory being deleted</param>
    /// <param name="userId">The ID of the owner of the directory</param>
    /// <param name="force">
    ///     If set to true the directory and all of it's content will be deleted,
    ///     if there are items in the directory, and
    ///     it is not set to true a DirectoryNotEmptyException will be thrown.
    ///     Defaults to false
    /// </param>
    /// <param name="ct">Token for cancellation</param>
    /// <exception cref="DirectoryNotEmptyException"></exception>
    public async Task DeleteDirectoryAsync(Guid id, Guid userId, bool force = false,
        CancellationToken ct = default)
    {
        var directory = await GetDirectoryByIdAsync(id, userId, ct);

        if (!force)
        {
            // Check if directory has children or files
            var childCount = await _unitOfWork.Directories.CountAsync(
                d => d.ParentId == id && d.DeletedAt == null, ct);
            
            var fileCount = await _unitOfWork.Files.CountAsync(
                f => f.DirectoryId == id && f.DeletedAt == null, ct);

            var totalItems = childCount + fileCount;
            if (totalItems > 0)
            {
                throw new DirectoryNotEmptyException(id, totalItems);
            }
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            if (force)
            {
                // Recursively delete all subdirectories and files
                await DeleteDirectoryRecursivelyAsync(id, ct);
            }
            
            _unitOfWork.Directories.Remove(directory);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> DirectoryExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _unitOfWork.Directories.ExistsAsync(d => d.Id == id && d.DeletedAt == null, ct);
    }

    public async Task<bool> DirectoryExistsWithOwnershipAsync(Guid id, Guid ownerId, CancellationToken ct = default)
    {
        return await _unitOfWork.Directories.ExistsAsync(d => d.Id == id && d.DeletedAt == null && d.OwnerId == ownerId, ct);
    }

    public async Task<bool> HasAccessToDirectoryAsync(Guid directoryId, Guid userId, 
        CancellationToken ct = default)
    {
        var directory = await _unitOfWork.Directories.GetByIdAsync(directoryId, ct);
        return directory != null && directory.OwnerId == userId;
    }

    // Private helper methods
    private async Task VerifyDirectoryAccessAsync(Guid directoryId, Guid userId, CancellationToken ct)
    {
        var directory = await _unitOfWork.Directories.GetByIdAsync(directoryId, ct);
        
        if (directory == null)
        {
            throw new DirectoryNotFoundException(directoryId);
        }

        if (directory.OwnerId != userId)
        {
            throw new UnauthorizedDirectoryAccessException(directoryId, userId);
        }
        
    }

    private static void ValidateDirectoryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidDirectoryNameException(name, "Directory name cannot be empty.");
        }

        if (name.Length > 255)
        {
            throw new InvalidDirectoryNameException(name, "Directory name cannot exceed 255 characters.");
        }

        if (name.IndexOfAny(InvalidChars) >= 0)
        {
            throw new InvalidDirectoryNameException(name, 
                $"Directory name contains invalid characters: {string.Join(", ", InvalidChars)}");
        }
    }

    private async Task<bool> WouldCreateCircularReferenceAsync(Guid directoryId, Guid newParentId, 
        CancellationToken ct)
    {
        var currentId = newParentId;
        
        while (currentId != Guid.Empty)
        {
            if (currentId == directoryId)
            {
                return true;
            }

            var parent = await _unitOfWork.Directories.GetByIdAsync(currentId, ct);
            if (parent?.ParentId == null)
            {
                break;
            }

            currentId = parent.ParentId.Value;
        }

        return false;
    }

    private async Task DeleteDirectoryRecursivelyAsync(Guid directoryId, CancellationToken ct)
    {
        // Get all subdirectories
        var subdirectories = await _unitOfWork.Directories.FindAsync(
            d => d.ParentId == directoryId && d.DeletedAt == null, ct);

        // Recursively delete subdirectories
        foreach (var subdir in subdirectories)
        {
            await DeleteDirectoryRecursivelyAsync(subdir.Id, ct);
        }

        // Delete all files in this directory
        var files = await _unitOfWork.Files.FindAsync(
            f => f.DirectoryId == directoryId && f.DeletedAt == null, ct);
        
        _unitOfWork.Files.RemoveRange(files);
    }
}