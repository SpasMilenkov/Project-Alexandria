using Common;
using Common.Services;
using DTO.Directories;
using DTO.Files;
using Models.Enumerators;
using Storage.Directories.Exceptions;
using Storage.Directories.TreeBuilder;
using Storage.Directories.TreeBuilder.Extensions;
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

    public async Task<Dictionary<string, Guid?>> CreateDirectorySubTreeAsync(List<string> paths, Guid? parentId,
        Guid userId, CancellationToken ct = default)
    {
        Directory? root = null;

        if (parentId is not null)
        {
            root = await _unitOfWork.Directories.GetByIdAsync((Guid)parentId, ct);

            if (root is null) throw new DirectoryNotFoundException((Guid)parentId);
        }

        var builder = new DirectoryTreeBuilder();
        var tree = DirectoryTreeBuilder.BuildDirectoryTree(paths, root?.Name ?? "root", root?.Id,
            isRoot: parentId == null);
        var fileToParentMapping = DirectoryTreeBuilder.BuildFileToParentMapping(paths, tree);

        // The root dir already exists, we should not insert it and it is always bubbling on top
        var nodes = builder.GetAllDirectories(tree).Skip(1);

        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            await _unitOfWork.Directories.AddRangeAsync(nodes.Select(n => n.ToDirectory(userId)), ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        return fileToParentMapping;
    }

    public async Task<Directory> GetDirectoryByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var directory = await _unitOfWork.Directories.GetByIdAsync(id, ct);

        if (directory == null)
        {
            throw new DirectoryNotFoundException(id);
        }

        return directory.OwnerId != userId ? throw new UnauthorizedDirectoryAccessException(id, userId) : directory;
    }

    public async Task<DirectorySummaryDto> GetDirectoryDtoByIdAsync(Guid id, Guid userId,
        CancellationToken ct = default)
    {
        var dir = await _unitOfWork.Directories.GetDirectoryMetadataAsync(id, userId, ct);

        if (dir is null) throw new DirectoryNotFoundException(id);

        return dir.ToSummaryDto();
    }

    public Task<PaginatedResult<DirectorySummaryDto>> FindDirectoryAsync(Guid userId, DirectorySearchQuery query,
        CancellationToken ct = default)
    {
        return _unitOfWork.Directories.FindDirectoryAsync(userId, query, ct);
    }

    public async Task<PaginatedResult<DirectorySummaryDto>> GetPaginatedDirectories(Guid id, Guid userId,
        int currentPage = 1,
        int pageSize = 25,
        SortDirection sortDirection = SortDirection.Asc,
        SortBy sortBy = SortBy.Name,
        CancellationToken ct = default)
    {
        return await _unitOfWork.Directories.GetSubdirectoriesAsync(id, userId, currentPage, pageSize, sortDirection,
            sortBy, ct);
    }

    public async Task<PaginatedResult<DirectorySummaryDto>> GetRootDirectoriesAsync(Guid ownerId, int page = 1,
        int pageSize = 25,
        SortDirection sortDirection = SortDirection.Asc,
        SortBy sortBy = SortBy.Name, CancellationToken ct = default)
    {
        return await _unitOfWork.Directories.GetRootDirectoriesAsync(ownerId, page, pageSize, sortBy, sortDirection,
            ct);
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

    public async Task MoveDirectoryAsync(Guid[] ids, Guid? newParentId, Guid userId,
        CancellationToken ct = default)
    {
        if (ids.Length == 0) return;

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.Directories.MoveDirectories(ids, newParentId, userId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
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
                await DeleteDirectoryRecursivelyAsync(id, userId, ct);
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
        return await _unitOfWork.Directories.ExistsAsync(d => d.Id == id && d.DeletedAt == null && d.OwnerId == ownerId,
            ct);
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

    private async Task DeleteDirectoryRecursivelyAsync(Guid directoryId, Guid userId, CancellationToken ct = default)
    {
        // Get all subdirectories
        var subdirectories = await _unitOfWork.Directories.FindAsync(
            d => d.ParentId == directoryId && d.DeletedAt == null, ct);

        // Recursively delete subdirectories
        foreach (var subdir in subdirectories) await DeleteDirectoryRecursivelyAsync(subdir.Id, userId, ct);

        // Delete all files in this directory
        var files = await _unitOfWork.Files.FindAsync(
            f => f.DirectoryId == directoryId && f.DeletedAt == null, ct);
        await _unitOfWork.Files.MarkAsDeleted(files.Select(f => f.Id).ToArray(), userId, ct);
    }

    public async Task CopyDirectoryAsync(Guid directoryId, Guid? destinationId, Guid userId,
        CancellationToken ct = default) =>
        await _unitOfWork.Directories.CopyDirectory(directoryId, destinationId, userId, ct);

    public async Task<int> RestoreDirectories(Guid[] directoryIds, Guid userId, CancellationToken ct = default)
    {
        return await _unitOfWork.Directories.RestoreDirectories(directoryIds, userId, ct);
    }
}
