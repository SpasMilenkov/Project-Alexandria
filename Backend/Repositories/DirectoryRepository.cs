using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Common.Repositories;
using Data.Context;
using DTO.Directories;
using DTO.Files;
using Directory = Models.Directory;
using File = Models.File;

namespace Repositories;

public class DirectoryRepository(AlexandriaDbContext context) : IDirectoryRepository
{
    private IDirectoryRepository _directoryRepositoryImplementation;

    public async Task<Directory?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Directories
            .FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null, ct);
    }

    public async Task<Directory?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Directories
            .AsSplitQuery()
            .AsNoTracking()
            .Include(d => d.Parent)
            .Include(d => d.Children)
            .Include(d => d.Files)
            .Include(d => d.Owner)
            .FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null, ct);   
    }

    public async Task<Directory?> GetDirectoryMetadataAsync(Guid id, Guid ownerId, CancellationToken ct = default)
    {
        return await context.Directories
            .AsNoTracking()
            .Include(d => d.Owner)
            .FirstOrDefaultAsync(d =>
            d.Id == id && d.OwnerId == ownerId && d.DeletedAt == null, ct);
    }

    public async Task<Directory?> FirstOrDefaultAsync(Expression<Func<Directory, bool>> predicate, CancellationToken ct = default)
    {
        return await context.Directories
            .FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IEnumerable<Directory>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.Directories
            .AsSplitQuery()
            .AsNoTracking()
            .Include(d => d.Parent)
            .Include(d => d.Children)
            .Include(d => d.Owner)
            .Where(d => d.DeletedAt == null)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Directory>> FindAsync(Expression<Func<Directory, bool>> predicate, CancellationToken ct = default)
    {
        return await context.Directories
            .AsSplitQuery()
            .Include(d => d.Parent)
            .Include(d => d.Children)
            .Include(d => d.Owner)
            .Where(predicate)
            .ToListAsync(ct);
    }

    public async Task<Directory> AddAsync(Directory entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        await context.Directories.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<IEnumerable<Directory>> AddRangeAsync(IEnumerable<Directory> entities, CancellationToken ct = default)
    {
        var directories = entities.ToList();
        var now = DateTime.UtcNow;
        
        foreach (var dir in directories)
        {
            dir.CreatedAt = now;
        }
        
        await context.Directories.AddRangeAsync(directories, ct);
        await context.SaveChangesAsync(ct);
        return directories;
    }

    public void Update(Directory entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        context.Directories.Update(entity);
        context.SaveChanges();
    }

    public void Remove(Directory entity)
    {
        entity.DeletedAt = DateTime.UtcNow;
        context.Directories.Update(entity);
        context.SaveChanges();
    }

    public void RemoveRange(IEnumerable<Directory> entities)
    {
        var now = DateTime.UtcNow;
        var directories = entities.ToList();
        
        foreach (var dir in directories)
        {
            dir.DeletedAt = now;
        }
        
        context.Directories.UpdateRange(directories);
        context.SaveChanges();
    }

    public async Task<int> CountAsync(Expression<Func<Directory, bool>>? predicate = null, CancellationToken ct = default)
    {
        if (predicate == null)
        {
            return await context.Directories
                .Where(d => d.DeletedAt == null)
                .CountAsync(ct);
        }
        
        return await context.Directories
            .Where(predicate)
            .CountAsync(ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<Directory, bool>> predicate, CancellationToken ct = default)
    {
        return await context.Directories.AnyAsync(predicate, ct);
    }
    
    public async Task<IEnumerable<Directory>> GetAllSubDirectoriesAsync(Guid directoryId, CancellationToken ct = default)
    {
        var allDirectories = new List<Directory>();
        var directoriesToProcess = new Queue<Guid>();
        directoriesToProcess.Enqueue(directoryId);

        while (directoriesToProcess.Count > 0)
        {
            var currentId = directoriesToProcess.Dequeue();
            var children = await context.Directories
                .Include(d => d.Owner)
                .Where(d => d.ParentId == currentId && d.DeletedAt == null)
                .ToListAsync(ct);

            foreach (var child in children)
            {
                allDirectories.Add(child);
                directoriesToProcess.Enqueue(child.Id);
            }
        }

        return allDirectories;
    }

    public async Task<IEnumerable<File>> GetAllFilesInDirectoryAsync(Guid directoryId, bool includeSubdirectories = false, CancellationToken ct = default)
    {
        if (!includeSubdirectories)
        {
            return await context.Files
                .Where(f => f.DirectoryId == directoryId && f.DeletedAt == null)
                .ToListAsync(ct);
        }

        var directoryIds = new List<Guid> { directoryId };
        var subdirectories = await GetAllSubDirectoriesAsync(directoryId, ct);
        directoryIds.AddRange(subdirectories.Select(d => d.Id));

        return await context.Files
            .Where(f => directoryIds.Contains(f.DirectoryId ?? Guid.Empty) && f.DeletedAt == null)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Directory>> GetUserDirectories(Guid ownerId,
        Guid? parentId,
        CancellationToken ct = default)
    {
        return await context.Directories
            .Where(d => d.OwnerId == ownerId && d.ParentId == parentId && d.DeletedAt == null)
            .ToListAsync(cancellationToken: ct);
    }

    public Task<List<Directory>> GetSubDirectories (Guid directoryId, CancellationToken ct)
    {
        return context.Directories
            .Where(d => d.ParentId == directoryId && d.DeletedAt == null)
            .ToListAsync(ct);
    }

    public async Task<RootContent> GetRootContentAsync(Guid ownerId, CancellationToken ct = default)
    {
        var directories = await context.Directories
            .AsNoTracking()
            .AsSplitQuery()
            .Include(d => d.Children)
            .Include(d => d.Owner)
            .Where(d => d.ParentId == null && d.OwnerId == ownerId && d.DeletedAt == null)
            .ToListAsync(ct);

        var files = await context.Files
            .AsNoTracking()
            .Where(f => f.DirectoryId == null && f.OwnerId == ownerId && f.DeletedAt == null)
            .ToListAsync(ct);

        return new RootContent
        {
            Directories = directories,
            Files = files
        };
    }

    public async Task<RootContentSummaryDto> GetRootContentSummaryAsync(Guid ownerId, CancellationToken ct = default)
    {
        var directories = await context.Directories
            .AsNoTracking()
            .Where(d => d.ParentId == null && d.OwnerId == ownerId && d.DeletedAt == null)
            .Select(d => new DirectorySummaryDto(
                d.Id,
                d.Name,
                d.ParentId,
                d.CreatedAt,
                d.UpdatedAt,
                new UserDto
                {
                    Id = d.Owner.Id,
                    Email = d.Owner.Email,
                    Name = d.Owner.Name
                }))
            .ToListAsync(ct);
        
        var files = await context.Files
            .AsNoTracking()
            .Where(f => f.DirectoryId == null && f.OwnerId == ownerId && f.DeletedAt == null)
            .Select(f => new FileSummary(
                f.Id,
                f.Name,
                f.MimeType,
                f.HasPreview,
                f.Path))
            .ToListAsync(ct);
        
        return new RootContentSummaryDto
        {
            Directories = directories,
            Files = files
        };
    }
    
    public async Task<string> GetDirectoryPathAsync(Guid directoryId, CancellationToken ct = default)
    {
        var pathParts = new List<string>();
        var currentDir = await context.Directories
            .Include(d => d.Parent)
            .FirstOrDefaultAsync(d => d.Id == directoryId && d.DeletedAt == null, ct);

        while (currentDir != null)
        {
            pathParts.Insert(0, currentDir.Name);
            
            if (currentDir.ParentId.HasValue)
            {
                currentDir = await context.Directories
                    .Include(d => d.Parent)
                    .FirstOrDefaultAsync(d => d.Id == currentDir.ParentId && d.DeletedAt == null, ct);
            }
            else
            {
                currentDir = null;
            }
        }

        return string.Join("/", pathParts);
    }
}