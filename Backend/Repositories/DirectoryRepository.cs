using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Common.Repositories;
using Data.Context;
using DTO.Directories;
using DTO.Files;
using DTO.Tags;
using Models.Enumerators;
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

    public async Task<Directory?> GetByIdWithDetailsAsync(Guid id,
        CancellationToken ct = default)
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

    public async Task<PaginatedResult<DirectorySummaryDto>> GetSubdirectoriesAsync(Guid parentDirectoryId,
        Guid userId,
        int currentPage = 1,
        int pageSize = 25,
        SortDirection sortDirection = SortDirection.Asc,
        SortBy sortBy = SortBy.Name, 
        CancellationToken ct = default)
    {
        var dbQuery = context.Directories.AsQueryable();

        dbQuery = dbQuery.Where(d => d.ParentId == parentDirectoryId && d.OwnerId == userId);
        
        dbQuery = sortBy switch
        {
            SortBy.Name => sortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(d => d.Name)
                : dbQuery.OrderByDescending(d => d.Name),

            SortBy.CreatedAt => sortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(d => d.CreatedAt)
                : dbQuery.OrderByDescending(d => d.CreatedAt),

            SortBy.UpdatedAt => sortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(d => d.UpdatedAt)
                : dbQuery.OrderByDescending(d => d.UpdatedAt),

            _ => dbQuery.OrderBy(d => d.Name)
        };

        var totalCount = await dbQuery.CountAsync(ct);
        
        var result = await dbQuery
            .AsNoTracking()
            .Skip((currentPage -1) * pageSize)
            .Take(pageSize)
            .Select(d => new DirectorySummaryDto(
                d.Id, d.Name, d.ParentId, d.CreatedAt, d.UpdatedAt, new UserDto
                {
                    Id = d.OwnerId,
                    Name = d.Owner.Name,
                    Email = d.Owner.Email
                })).ToListAsync(cancellationToken: ct);

        return new PaginatedResult<DirectorySummaryDto>
        {
            Items = result,
            CurrentPage = currentPage,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
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

    public async Task<PaginatedResult<DirectorySummaryDto>> FindDirectoryAsync(Guid userId, DirectorySearchQuery query, CancellationToken ct)
    {
        if (query.CurrentPage < 0)
            throw new ArgumentException("Page number must be non-negative", nameof(query.CurrentPage));

        if (query.PageSize <= 0 || query.PageSize > 100)
            throw new ArgumentException("Page size must be between 1 and 100", nameof(query.PageSize));

        var dbQuery = context.Directories.AsQueryable();
        
        //When file and directory sharing comes later on this will be optional in combination
        //with the new access policies for Now users have no access to directories of other users
        dbQuery = dbQuery.Where(d => d.OwnerId == userId);
        if (query.IsDeleted)
        {
            dbQuery = dbQuery.Where(d => d.DeletedAt != null);
        }
        else
        {
            dbQuery = dbQuery.Where(d => d.DeletedAt == null);
        }
        
        if (query.DeletedAt.HasValue)
        {
            var startOfDay = query.DeletedAt.Value.Date;
            var endOfDay = startOfDay.AddDays(1);
            dbQuery = dbQuery.Where(d => d.DeletedAt >= startOfDay && d.DeletedAt < endOfDay);
        }

        if (query.DirectoryId.HasValue)
        {
            dbQuery = dbQuery.Where(d => d.Id == query.DirectoryId.Value);
        }

        if (query.OwnerId.HasValue)
        {
            dbQuery = dbQuery.Where(d => d.OwnerId == query.OwnerId.Value);
        }

        // if (query.IsShared.HasValue)
        // {
        //     dbQuery = dbQuery.Where(d => d.IsShared == query.IsShared.Value);
        // }

        if (query.CreatedBefore.HasValue)
        {
            dbQuery = dbQuery.Where(d => d.CreatedAt <= query.CreatedBefore.Value);
        }
        
        if (query.CreatedAfter.HasValue)
        {
            dbQuery = dbQuery.Where(d => d.CreatedAt >= query.CreatedAfter.Value);
        }

        if (query.UpdatedBefore.HasValue)
        {
            dbQuery = dbQuery.Where(d => d.UpdatedAt <= query.UpdatedBefore.Value);
        }

        if (query.UpdatedAfter.HasValue)
        {
            dbQuery = dbQuery.Where(d => d.UpdatedAt >= query.UpdatedAfter.Value);
        }

        if (query.HasFiles.HasValue)
        {
            if (query.HasFiles.Value)
            {
                dbQuery = dbQuery.Where(d => d.Files.Any());
            }
            else
            {
                dbQuery = dbQuery.Where(d => !d.Files.Any());
            }
        }

        if (query.HasSubdirectories.HasValue)
        {
            if (query.HasSubdirectories.Value)
            {
                dbQuery = dbQuery.Where(d => d.Children.Any());
            }
            else
            {
                dbQuery = dbQuery.Where(d => !d.Children.Any());
            }
        }
        
        if (!string.IsNullOrWhiteSpace(query.NameContains))
        {
            var searchTerm = query.NameContains.Trim();
            
            // Lower threshold for short strings
            var similarityThreshold = searchTerm.Length <= 3 ? 0.09 : 0.3;
            
            dbQuery = dbQuery   
                .Where(d => 
                    EF.Functions.ILike(d.Name, $"%{searchTerm}%"))
                .OrderByDescending(d => 
                    EF.Functions.ILike(d.Name, $"{searchTerm}%") ? 3 :      // Starts with (exact)
                    EF.Functions.ILike(d.Name, $"%{searchTerm}%") ? 2 :     // Contains
                    EF.Functions.TrigramsSimilarity(d.Name, searchTerm) > similarityThreshold ? 1 : 0); // Fuzzy
        }

        if (query.ParentDirectoryId.HasValue)
        {
            dbQuery = dbQuery.Where(d => d.ParentId == query.ParentDirectoryId.Value);
        }

        // if (query.IsStarred)
        // {
        //     dbQuery = dbQuery.Where(d => d.IsStarred == true);
        // }
        
        var totalCount = await dbQuery.CountAsync(ct);
        
        dbQuery = query.SortBy switch
        {
            SortBy.Name => query.SortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(d => d.Name)
                : dbQuery.OrderByDescending(d => d.Name),

            SortBy.CreatedAt => query.SortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(d => d.CreatedAt)
                : dbQuery.OrderByDescending(d => d.CreatedAt),

            SortBy.UpdatedAt => query.SortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(d => d.UpdatedAt)
                : dbQuery.OrderByDescending(d => d.UpdatedAt),

            _ => dbQuery.OrderBy(d => d.Name)
        };
        
        var items = await dbQuery
            .AsNoTracking()
            .Skip(query.CurrentPage * query.PageSize)
            .Take(query.PageSize)
            .Select(d => new DirectorySummaryDto
            (
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
                }
            ))
            .ToListAsync(ct);
        
        return new PaginatedResult<DirectorySummaryDto>()
        {
            Items = items,
            TotalCount = totalCount,
            CurrentPage = query.CurrentPage,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
        };
    }
        
    public async Task<PaginatedResult<DirectorySummaryDto>> GetRootDirectoriesAsync(
        Guid ownerId, 
        int page = 1,
        int pageSize = 25,
        SortBy sortBy = SortBy.Name,
        SortDirection sortDirection = SortDirection.Asc,
        CancellationToken ct = default)
    {
        var query = context.Directories
            .AsNoTracking()
            .Where(d => d.ParentId == null && d.OwnerId == ownerId && d.DeletedAt == null);
        
        var totalCount = await query.CountAsync(ct);

        var directories = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
        
        return new PaginatedResult<DirectorySummaryDto>
        {
            Items = directories,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<PaginatedResult<FileResult>> GetRootFilesAsync(
        Guid ownerId,
        int page = 1,
        int pageSize = 25,
        SortBy sortBy = SortBy.Name,
        SortDirection sortDirection = SortDirection.Asc,
        CancellationToken ct = default)
    {
        var dbQuery = context.Files
            .AsNoTracking()
            .Where(f => f.DirectoryId == null && f.OwnerId == ownerId && f.DeletedAt == null);
        
        dbQuery = sortBy switch
        {
            SortBy.Name => sortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(d => d.Name)
                : dbQuery.OrderByDescending(d => d.Name),

            SortBy.CreatedAt => sortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(d => d.CreatedAt)
                : dbQuery.OrderByDescending(d => d.CreatedAt),

            SortBy.UpdatedAt => sortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(d => d.UpdatedAt)
                : dbQuery.OrderByDescending(d => d.UpdatedAt),

            _ => dbQuery.OrderBy(d => d.Name)
        };
        
        var totalCount = await dbQuery.CountAsync(ct);
        
        var files = await dbQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FileResult(
                f.Id, 
                f.Name, 
                f.MimeType, 
                f.CreatedAt, 
                f.UpdatedAt, 
                f.DeletedAt,
                new FileVersionDto(
                    f.CurrentVersion.Id, 
                    f.CurrentVersion.Size, 
                    f.CurrentVersion.MimeType, 
                    f.CurrentVersion.VersionNumber),
                f.Tags.Select(t => new TagDto
                {
                    Id = t.Id,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt,
                    Name = t.Name,
                    UserId = t.OwnerId
                }).ToList(),
                new UserDto
                {
                    Id = f.OwnerId,
                    Name = f.Owner.Name,
                    Email = f.Owner.Email
                }
            ))
            .ToListAsync(ct);
        
        return new PaginatedResult<FileResult>
        {
            Items = files,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
    
    public async Task<List<PathPartDto>> GetDirectoryPathAsync(Guid directoryId, CancellationToken ct = default)
    {
        var pathParts = new List<PathPartDto>();
        
        var currentDir = await context.Directories
            .Include(d => d.Parent)
            .FirstOrDefaultAsync(d => d.Id == directoryId && d.DeletedAt == null, ct);

        while (currentDir != null)  
        {
            pathParts.Insert(0, new PathPartDto(currentDir.Id, currentDir.Name));
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

        return pathParts;
    }
}