using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Directories;
using Alexandria.Dto.Files;
using Alexandria.Repositories.Projections;
using Microsoft.EntityFrameworkCore;
using Directory = Alexandria.Data.Models.Directory;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Repositories;

public class DirectoryRepository(AlexandriaDbContext context) : IDirectoryRepository
{
    public async Task<Directory?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Directories
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

    public async Task<Directory?> FirstOrDefaultAsync(Expression<Func<Directory, bool>> predicate,
        CancellationToken ct = default)
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

    public async Task<IEnumerable<Directory>> FindAsync(Expression<Func<Directory, bool>> predicate,
        CancellationToken ct = default)
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

    public async Task<IEnumerable<Directory>> AddRangeAsync(IEnumerable<Directory> entities,
        CancellationToken ct = default)
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

    public async Task<int> CountAsync(Expression<Func<Directory, bool>>? predicate = null,
        CancellationToken ct = default)
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

    public async Task<IEnumerable<Directory>> GetAllSubDirectoriesAsync(Guid directoryId,
        CancellationToken ct = default)
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

        dbQuery = dbQuery.Where(d => d.ParentId == parentDirectoryId && d.OwnerId == userId && d.DeletedAt == null);

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
            .Skip((currentPage - 1) * pageSize)
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

    public async Task<IEnumerable<File>> GetAllFilesInDirectoryAsync(Guid directoryId,
        bool includeSubdirectories = false, CancellationToken ct = default)
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
        Guid? parentId = null,
        CancellationToken ct = default)
    {
        return await context.Directories
            .Where(d => d.OwnerId == ownerId && d.ParentId == parentId && d.DeletedAt == null)
            .ToListAsync(cancellationToken: ct);
    }

    public Task<List<Directory>> GetSubDirectories(Guid directoryId, CancellationToken ct = default)
    {
        return context.Directories
            .Where(d => d.ParentId == directoryId && d.DeletedAt == null)
            .ToListAsync(ct);
    }

    public async Task<PaginatedResult<DirectorySummaryDto>> FindDirectoryAsync(Guid userId, DirectorySearchQuery query,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(query.CurrentPage);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.PageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.PageSize, 100);

        var dbQuery = context.Directories.AsQueryable();
        var isSearching = !string.IsNullOrWhiteSpace(query.NameContains);

        if (isSearching)
        {
            var searchTerm = query.NameContains!.Trim().ToLower();

            dbQuery = context.Directories.FromSqlInterpolated($@"
                    SELECT *,
                        (similarity(""NormalizedName"", {searchTerm}) * 2.0 +
                         ts_rank(""SearchVector"", plainto_tsquery('simple', {searchTerm}))) as search_score
                    FROM ""Directories""
                    WHERE
                        similarity(""NormalizedName"", {searchTerm}) > 0.1
                        OR
                        ""SearchVector"" @@ plainto_tsquery('simple', {searchTerm})
                    ORDER BY
                        search_score DESC
                ");
        }

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

        if (query.DeletedAfter.HasValue)
        {
            var dateUtc = DateTime.SpecifyKind(query.DeletedAfter.Value.Date, DateTimeKind.Utc);
            dbQuery = dbQuery.Where(d => d.CreatedAt >= dateUtc);
        }

        if (query.DeletedBefore.HasValue)
        {
            var nextDayUtc = DateTime.SpecifyKind(query.DeletedBefore.Value.Date.AddDays(1), DateTimeKind.Utc);
            dbQuery = dbQuery.Where(d => d.CreatedAt < nextDayUtc);
        }

        if (query.DirectoryId.HasValue)
        {
            dbQuery = dbQuery.Where(d => d.Id == query.DirectoryId.Value);
        }

        if (query.OwnerId.HasValue)
        {
            dbQuery = dbQuery.Where(d => d.OwnerId == query.OwnerId.Value);
        }

        // --- Created Filters ---
        if (query.CreatedAfter.HasValue)
        {
            // Start of the day (00:00:00) on the selected date
            var startOfAfterDay = DateTime.SpecifyKind(query.CreatedAfter.Value.Date, DateTimeKind.Utc);
            dbQuery = dbQuery.Where(d => d.CreatedAt >= startOfAfterDay);
        }

        if (query.CreatedBefore.HasValue)
        {
            // Start of the NEXT day (00:00:00)
            // This effectively captures everything UP TO 23:59:59 on the 'Before' day
            var startOfNextDay = DateTime.SpecifyKind(query.CreatedBefore.Value.Date.AddDays(1), DateTimeKind.Utc);
            dbQuery = dbQuery.Where(d => d.CreatedAt < startOfNextDay);
        }

        // --- Updated Filters ---
        if (query.UpdatedAfter.HasValue)
        {
            var startOfAfterDay = DateTime.SpecifyKind(query.UpdatedAfter.Value.Date, DateTimeKind.Utc);
            dbQuery = dbQuery.Where(d => d.UpdatedAt >= startOfAfterDay);
        }

        if (query.UpdatedBefore.HasValue)
        {
            var startOfNextDay = DateTime.SpecifyKind(query.UpdatedBefore.Value.Date.AddDays(1), DateTimeKind.Utc);
            dbQuery = dbQuery.Where(d => d.UpdatedAt < startOfNextDay);
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

        if (!isSearching)
        {
            dbQuery = query.SortBy switch
            {
                SortBy.Name => query.SortDirection == SortDirection.Asc
                    ? dbQuery.OrderBy(f => f.Name)
                    : dbQuery.OrderByDescending(f => f.Name),

                SortBy.CreatedAt => query.SortDirection == SortDirection.Asc
                    ? dbQuery.OrderBy(f => f.CreatedAt)
                    : dbQuery.OrderByDescending(f => f.CreatedAt),

                SortBy.UpdatedAt => query.SortDirection == SortDirection.Asc
                    ? dbQuery.OrderBy(f => f.UpdatedAt)
                    : dbQuery.OrderByDescending(f => f.UpdatedAt),

                _ => dbQuery.OrderBy(f => f.Name)
            };
        }


        if (query.ParentDirectoryId.HasValue)
        {
            dbQuery = dbQuery.Where(d => d.ParentId == query.ParentDirectoryId.Value);
        }

        var totalCount = await dbQuery.CountAsync(ct);

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
        var dbQuery = context.Directories
            .AsNoTracking()
            .Where(d => d.ParentId == null && d.OwnerId == ownerId && d.DeletedAt == null);

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

        var directories = await dbQuery
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
            .Select(FileProjections.ToFileResult)
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

    private static string ResolveNameDuplicate(HashSet<string> names, string directoryName)
    {
        var newName = directoryName + "Copy";

        if (!names.Contains(newName)) return newName;
        var counter = 2;

        while (names.Contains(newName))
        {
            newName = directoryName + $"({counter})";
            counter++;
        }

        return newName;
    }

    public async Task CopyDirectory(Guid directoryId, Guid? destinationId, Guid userId, CancellationToken ct = default)
    {
        var dir = await context.Directories
            .Where(d => d.Id == directoryId && d.OwnerId == userId)
            .FirstOrDefaultAsync(ct);

        if (dir is null) throw new DirectoryNotFoundException();

        var existingDirNames = await context.Directories
            .Where(d => d.ParentId == destinationId && d.OwnerId == userId && d.DeletedAt == null)
            .Select(d => d.Name)
            .ToHashSetAsync(ct);

        var directories = await context.Directories.FromSqlInterpolated(
                $"""
                 WITH RECURSIVE RecursiveDirs AS (
                     SELECT d.*, ARRAY[d."Id"] AS path
                     FROM "Directories" d
                     WHERE d."Id" = {directoryId}
                       AND d."OwnerId" = {userId}

                     UNION ALL

                     SELECT d.*, rd.path || d."Id"
                     FROM "Directories" d
                     JOIN RecursiveDirs rd ON d."ParentId" = rd."Id"
                     WHERE NOT d."Id" = ANY(rd.path)
                 )
                 SELECT
                     "Id", "Name", "NormalizedName", "SearchVector",
                     "ParentId", "CreatedAt", "UpdatedAt", "DeletedAt",
                     "UpdatedBy", "OwnerId"
                 FROM RecursiveDirs;
                 """)
            .AsNoTracking()
            .ToListAsync(ct);

        var dirIds = directories.Select(d => d.Id).ToList();

        var files = await context.Files
            .Where(f => f.DirectoryId != null && dirIds.Contains(f.DirectoryId ?? Guid.Empty) && f.OwnerId == userId)
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.MimeType,
                f.DirectoryId,
                Version = new
                {
                    f.CurrentVersion.ContentObjectId,
                    f.CurrentVersion.Size,
                    f.CurrentVersion.MimeType,
                    f.CurrentVersion.ContentHash
                }
            })
            .ToListAsync(ct);

        var dirMap = directories.ToDictionary(d => d.Id, _ => Guid.NewGuid());
        var now = DateTime.UtcNow;

        var newDirectories = directories.Select(d => new Directory
        {
            Id = dirMap[d.Id],
            Name = d.Id == directoryId
                ? ResolveNameDuplicate(existingDirNames, d.Name)
                : d.Name,
            ParentId = d.Id == directoryId
                ? destinationId
                : dirMap[d.ParentId!.Value],
            CreatedAt = now,
            OwnerId = userId
        }).ToList();

        var newFiles = new List<File>();
        var newVersions = new List<FileVersion>();

        foreach (var f in files)
        {
            var fileId = Guid.NewGuid();
            var versionId = Guid.NewGuid();

            newFiles.Add(new File
            {
                Id = fileId,
                Name = f.Name,
                MimeType = f.MimeType,
                DirectoryId = dirMap[f.DirectoryId!.Value],
                OwnerId = userId,
                CreatedAt = now,
                CurrentVersionId = null // ← set after versions are inserted
            });

            newVersions.Add(new FileVersion
            {
                Id = versionId,
                FileId = fileId,
                ContentObjectId = f.Version.ContentObjectId,
                ContentHash = f.Version.ContentHash,
                Size = f.Version.Size,
                MimeType = f.Version.MimeType,
                VersionNumber = 1,
                CreatedAt = now,
                CreatedBy = userId
            });
        }

        var existingTransaction = context.Database.CurrentTransaction;
        await using var tx = existingTransaction is null
            ? await context.Database.BeginTransactionAsync(ct)
            : null;

        try
        {
            // 1. Insert directories
            context.Directories.AddRange(newDirectories);
            await context.SaveChangesAsync(ct);

            // 2. Insert files (CurrentVersionId = null) + versions — no circular dependency
            context.Files.AddRange(newFiles);
            context.FileVersions.AddRange(newVersions);
            await context.SaveChangesAsync(ct);

            // 3. Wire up CurrentVersionId and update
            var versionLookup = newVersions.ToDictionary(v => v.FileId, v => v.Id);
            foreach (var file in newFiles)
                file.CurrentVersionId = versionLookup[file.Id];

            await context.SaveChangesAsync(ct);

            if (tx is not null)
                await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx is not null)
                await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task MoveDirectories(Guid[] ids, Guid? destinationId, Guid userId, CancellationToken ct = default)
    {
        // 1. Validation: Only run if moving to a specific folder.
        // If destinationId is null, we skip this as 'Root' always exists.
        if (destinationId.HasValue)
        {
            _ = await context.Directories.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == destinationId && d.OwnerId == userId, ct)
                ?? throw new InvalidOperationException("Invalid destination");
        }

        // 2. The Unified Query
        var rowsAffected = await context.Database.ExecuteSqlInterpolatedAsync($"""
                                                                               WITH RECURSIVE subtree AS (
                                                                                   SELECT "Id"
                                                                                   FROM "Directories"
                                                                                   WHERE "Id" = ANY({ids})
                                                                                     AND "OwnerId" = {userId}
                                                                                     AND "DeletedAt" IS NULL

                                                                                   UNION ALL

                                                                                   SELECT d."Id"
                                                                                   FROM "Directories" d
                                                                                   JOIN subtree s ON d."ParentId" = s."Id"
                                                                                   WHERE d."DeletedAt" IS NULL
                                                                               )
                                                                               UPDATE "Directories"
                                                                               SET
                                                                                   "ParentId" = {destinationId}::uuid,
                                                                                   "UpdatedAt" = NOW(),
                                                                                   "UpdatedBy" = {userId}
                                                                               WHERE "Id" = ANY({ids})
                                                                                 AND "OwnerId" = {userId}
                                                                                 AND "DeletedAt" IS NULL
                                                                                 AND (
                                                                                     {destinationId}::uuid IS NULL
                                                                                     OR
                                                                                     NOT EXISTS (SELECT 1 FROM subtree WHERE "Id" = {destinationId}::uuid)
                                                                                 )
                                                                               """, ct);

        if (rowsAffected == 0)
            throw new InvalidOperationException("Move failed: Directories not found or circular reference detected.");
    }

    public async Task<int> RestoreDirectories(
        Guid[] ids,
        Guid userId,
        CancellationToken ct = default)
    {
        var rowsAffected = await context.Database.ExecuteSqlInterpolatedAsync($"""
                                                                               WITH RECURSIVE subtree AS (
                                                                                   -- Root directories (must be within 30 days)
                                                                                   SELECT "Id"
                                                                                   FROM "Directories"
                                                                                   WHERE "Id" = ANY({ids})
                                                                                     AND "OwnerId" = {userId}
                                                                                     AND "DeletedAt" IS NOT NULL
                                                                                     AND "DeletedAt" > NOW() - INTERVAL '30 days'
                                                                                   UNION ALL
                                                                                   -- All children recursively (also within 30 days)
                                                                                   SELECT d."Id"
                                                                                   FROM "Directories" d
                                                                                   JOIN subtree s ON d."ParentId" = s."Id"
                                                                                   WHERE d."DeletedAt" IS NOT NULL
                                                                                     AND d."DeletedAt" > NOW() - INTERVAL '30 days'
                                                                               ),
                                                                               restored_directories AS (
                                                                                   UPDATE "Directories"
                                                                                   SET
                                                                                       "DeletedAt" = NULL,
                                                                                       "UpdatedAt" = NOW(),
                                                                                       "UpdatedBy" = {userId}
                                                                                   WHERE "Id" IN (SELECT "Id" FROM subtree)
                                                                                   RETURNING "Id"
                                                                               ),
                                                                               restored_files AS (
                                                                                   UPDATE "Files"
                                                                                   SET
                                                                                       "DeletedAt" = NULL,
                                                                                       "UpdatedAt" = NOW(),
                                                                                       "UpdatedBy" = {userId}
                                                                                   WHERE "DirectoryId" IN (SELECT "Id" FROM subtree)
                                                                                     AND "OwnerId" = {userId}
                                                                                     AND "DeletedAt" IS NOT NULL
                                                                                     AND "DeletedAt" > NOW() - INTERVAL '30 days'
                                                                                   RETURNING "Id"
                                                                               ),
                                                                               restored_versions AS (
                                                                                   UPDATE "FileVersions"
                                                                                   SET
                                                                                       "DeletedAt" = NULL,
                                                                                       "UpdatedAt" = NOW(),
                                                                                       "UpdatedBy" = {userId}
                                                                                   WHERE "FileId" IN (SELECT "Id" FROM restored_files)
                                                                                     AND "DeletedAt" IS NOT NULL
                                                                                     AND "DeletedAt" > NOW() - INTERVAL '30 days'
                                                                                   RETURNING "Id"
                                                                               )
                                                                               SELECT
                                                                                   (SELECT COUNT(*) FROM restored_directories) +
                                                                                   (SELECT COUNT(*) FROM restored_files) +
                                                                                   (SELECT COUNT(*) FROM restored_versions)
                                                                               """, ct);

        if (rowsAffected == 0)
            throw new InvalidOperationException("Restore failed or nothing to restore.");

        return rowsAffected;
    }

    public async Task<List<BulkDownloadEntry>> GetBulkDownloadEntriesAsync(
        Guid[] directoryIds,
        Guid[] fileIds,
        Guid userId,
        CancellationToken ct = default)
    {
        // ANY(ARRAY[]::uuid[]) = FALSE in Postgres — no conditional branching needed
        return await context.Database.SqlQuery<BulkDownloadEntry>($"""
                                                                   WITH RECURSIVE dir_tree AS (
                                                                       SELECT
                                                                           d."Id",
                                                                           d."Name",
                                                                           d."ParentId",
                                                                           d."Name"::text AS "RelativePath"
                                                                       FROM "Directories" d
                                                                       WHERE  d."Id"      = ANY({directoryIds})
                                                                         AND  d."OwnerId"   = {userId}
                                                                         AND  d."DeletedAt" IS NULL
                                                                       UNION ALL
                                                                       SELECT
                                                                           d."Id",
                                                                           d."Name",
                                                                           d."ParentId",
                                                                           dt."RelativePath" || '/' || d."Name"
                                                                       FROM "Directories" d
                                                                       JOIN dir_tree dt ON d."ParentId" = dt."Id"
                                                                       WHERE d."DeletedAt" IS NULL
                                                                   ),
                                                                   dir_files AS (
                                                                       SELECT
                                                                           f."Id"            AS "FileId",
                                                                           f."Name"          AS "FileName",
                                                                           dt."RelativePath" AS "DirectoryPath"
                                                                       FROM "Files" f
                                                                       JOIN dir_tree dt ON f."DirectoryId" = dt."Id"
                                                                       WHERE f."DeletedAt" IS NULL
                                                                         AND f."OwnerId"   = {userId}
                                                                   ),
                                                                   loose_files AS (
                                                                       SELECT
                                                                           f."Id"   AS "FileId",
                                                                           f."Name" AS "FileName",
                                                                           ''::text AS "DirectoryPath"
                                                                       FROM "Files" f
                                                                       WHERE f."Id"      = ANY({fileIds})
                                                                         AND f."OwnerId"   = {userId}
                                                                         AND f."DeletedAt" IS NULL
                                                                   ),
                                                                   all_files AS (
                                                                       SELECT * FROM dir_files
                                                                       UNION
                                                                       SELECT * FROM loose_files
                                                                   )
                                                                   SELECT
                                                                       af."FileId",
                                                                       af."FileName",
                                                                       af."DirectoryPath",
                                                                       co."StorageKey",
                                                                       co."IsPromoted",
                                                                       u."TempObjectKey",
                                                                       fv."IsEncrypted",
                                                                       fv."EncryptionIv",
                                                                       fv."EncryptionSalt",
                                                                       fv."IntegrityTag",
                                                                       fv."EncryptionHint",
                                                                       fv."IterationCount"
                                                                   FROM all_files af
                                                                   JOIN "Files"          f  ON f."Id"  = af."FileId"
                                                                   JOIN "FileVersions"   fv ON fv."Id" = f."CurrentVersionId"
                                                                   JOIN "ContentObjects" co ON co."Id" = fv."ContentObjectId"
                                                                   LEFT JOIN "Uploads"   u
                                                                          ON u."Hash"        = fv."ContentHash"
                                                                          AND u."Status" = 'Finished'
                                                                          AND co."IsPromoted" = FALSE
                                                                   """)
            .ToListAsync(ct);
    }
}