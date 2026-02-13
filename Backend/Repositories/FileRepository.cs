using System.Linq.Expressions;
using Common.Repositories;
using Data.Context;
using DTO.Files;
using DTO.Tags;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Enumerators;
using Repositories.Projections;
using File = Models.File;

namespace Repositories;

public class FileRepository(AlexandriaDbContext context) : IFileRepository
{
    private readonly DbSet<File> _files = context.Files;
    private readonly DbSet<FileVersion> _fileVersions = context.FileVersions;

    public async Task<File?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _files
            .Where(f => f.DeletedAt == null)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<PaginatedResult<FileResult>> FindFilesByTagsAsync(FileTagSearchQuery query,
        CancellationToken ct = default)
    {
        var filesQuery = _files
            .Include(f => f.Tags)
            .Where(f => f.DeletedAt == null);

        // Apply tag filtering based on match type
        filesQuery = query.MatchType switch
        {
            // ANY: File has at least one of the specified tags
            TagMatchType.Any => filesQuery.Where(f =>
                f.Tags.Any(t => query.TagIds.Contains(t.Id) && t.DeletedAt == null)),

            // ALL: File has all the specified tags
            TagMatchType.All => ApplyAllTagsFilter(filesQuery, query.TagIds),

            // EXACT: File has exactly these tags, no more, no less
            TagMatchType.Exact => filesQuery.Where(f =>
                f.Tags.Count(t => t.DeletedAt == null) == query.TagIds.Count &&
                f.Tags.Count(t => query.TagIds.Contains(t.Id) && t.DeletedAt == null) == query.TagIds.Count),

            _ => throw new ArgumentException("Invalid match type", nameof(query.MatchType))
        };

        // Apply user filter - files that have at least one tag from this user
        if (query.UserId.HasValue)
        {
            filesQuery = filesQuery.Where(f =>
                f.Tags.Any(t => t.OwnerId == query.UserId.Value && t.DeletedAt == null));
        }

        // Apply MIME type filter
        if (!string.IsNullOrWhiteSpace(query.MimeTypePrefix))
        {
            filesQuery = filesQuery.Where(f => f.MimeType.StartsWith(query.MimeTypePrefix));
        }

        // Apply date range filters
        if (query.CreatedAfter.HasValue)
        {
            filesQuery = filesQuery.Where(f => f.CreatedAt >= query.CreatedAfter.Value);
        }

        if (query.CreatedBefore.HasValue)
        {
            filesQuery = filesQuery.Where(f => f.CreatedAt <= query.CreatedBefore.Value);
        }

        var totalCount = await filesQuery.CountAsync(ct);

        var files = await filesQuery
            .OrderByDescending(f => f.CreatedAt)
            .Skip(query.CurrentPage * query.PageSize)
            .Take(query.PageSize)
            .Select(FileProjections.ToFileResult)
            .ToListAsync(ct);

        return new PaginatedResult<FileResult>
        {
            Items = files.ToList(),
            CurrentPage = query.CurrentPage,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
        };
    }

    private static IQueryable<File> ApplyAllTagsFilter(IQueryable<File> query, ICollection<Guid> tagIds)
    {
        foreach (var tagId in tagIds)
        {
            // Capture the tagId in a local variable to avoid closure issues
            var currentTagId = tagId;
            query = query.Where(f =>
                f.Tags.Any(t => t.Id == currentTagId && t.DeletedAt == null));
        }

        return query;
    }

    public async Task<File?> GetFileWithPreviewAsync(Guid id, CancellationToken ct = default)
    {
        return await _files
            .AsNoTracking()
            .Include(f => f.Preview)
            .Where(f => f.DeletedAt == null)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task HasDuplicatesAsync(Guid[] fileIds, Guid destinationId, Guid userId,
        CancellationToken ct = default)
    {
        var hasDuplicates = await _files.AnyAsync(f =>
                f.OwnerId == userId &&
                f.DirectoryId == destinationId &&
                _files.Any(src =>
                    fileIds.Contains(src.Id) &&
                    src.OwnerId == userId &&
                    src.Name == f.Name),
            ct);
        if (hasDuplicates)
            throw new InvalidOperationException("Files with the same names already exist in destination");
    }

    public async Task MarkAsDeleted(Guid[] fileIds, Guid userId, CancellationToken ct = default)
    {
        await _files
            .Where(f => f.OwnerId == userId && fileIds.Contains(f.Id))
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(f => f.DeletedAt, _ => DateTime.UtcNow)
                    .SetProperty(f => f.UpdatedBy, _ => userId),
                ct);
    }

    public async Task<bool> IsPromoted(Guid fileId, CancellationToken ct = default)
    {
        return await _files.Where(f => f.Id == fileId)
            .Select(f => f.CurrentVersion.ContentObject.IsPromoted)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaginatedResult<FileResult>> FindFiles(FileSearchQuery query, Guid userId,
        CancellationToken ct = default)
    {
        if (query.CurrentPage < 0)
            throw new ArgumentException("Page number must be non-negative", nameof(query.CurrentPage));

        if (query.PageSize is <= 0 or > 100)
            throw new ArgumentException("Page size must be between 1 and 100", nameof(query.PageSize));

        IQueryable<File> dbQuery;

        // Check for text search FIRST
        var isSearching = !string.IsNullOrWhiteSpace(query.NameContains);

        if (isSearching)
        {
            var searchTerm = query.NameContains!.Trim().ToLower();

            dbQuery = context.Files.FromSqlInterpolated($@"
                    SELECT *, 
                        (similarity(""NormalizedName"", {searchTerm}) * 2.0 + 
                         ts_rank(""SearchVector"", plainto_tsquery('simple', {searchTerm}))) as search_score
                    FROM ""Files""
                    WHERE 
                        similarity(""NormalizedName"", {searchTerm}) > 0.1
                        OR
                        ""SearchVector"" @@ plainto_tsquery('simple', {searchTerm})
                    ORDER BY 
                        search_score DESC
                ");
        }
        else
        {
            dbQuery = context.Files.AsQueryable();
        }

        // User ownership filter - always applied
        dbQuery = dbQuery.Where(f => f.OwnerId == userId);

        // Deletion filters
        if (query.OnlyDeleted || query.IsDeleted)
        {
            dbQuery = dbQuery.Where(f => f.DeletedAt != null);
        }
        else
        {
            dbQuery = dbQuery.Where(f => f.DeletedAt == null);
        }

        if (query.DeletedAfter.HasValue)
        {
            var dateUtc = DateTime.SpecifyKind(query.DeletedAfter.Value.Date, DateTimeKind.Utc);
            dbQuery = dbQuery.Where(f => f.CreatedAt >= dateUtc);
        }

        if (query.DeletedBefore.HasValue)
        {
            var nextDayUtc = DateTime.SpecifyKind(query.DeletedBefore.Value.Date.AddDays(1), DateTimeKind.Utc);
            dbQuery = dbQuery.Where(f => f.CreatedAt < nextDayUtc);
        }

        // Identity & structure filters
        if (query.DirectoryId.HasValue)
        {
            dbQuery = dbQuery.Where(f => f.Id == query.DirectoryId.Value);
        }

        if (query.ParentDirectoryId.HasValue)
        {
            dbQuery = dbQuery.Where(f => f.DirectoryId == query.ParentDirectoryId.Value);
        }

        if (query.OwnerId.HasValue)
        {
            dbQuery = dbQuery.Where(f => f.OwnerId == query.OwnerId.Value);
        }

        // File-specific filters
        if (query.MinSize.HasValue)
        {
            dbQuery = dbQuery.Where(f => f.CurrentVersion.Size >= query.MinSize.Value);
        }

        if (query.MaxSize.HasValue)
        {
            dbQuery = dbQuery.Where(f => f.CurrentVersion.Size <= query.MaxSize.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.MimeType))
        {
            dbQuery = dbQuery.Where(f => f.MimeType == query.MimeType.Trim());
        }

        // Time filters
        if (query.CreatedAfter.HasValue)
        {
            var dateUtc = DateTime.SpecifyKind(query.CreatedAfter.Value.Date, DateTimeKind.Utc);
            dbQuery = dbQuery.Where(f => f.CreatedAt >= dateUtc);
        }

        if (query.CreatedBefore.HasValue)
        {
            var nextDayUtc = DateTime.SpecifyKind(query.CreatedBefore.Value.Date.AddDays(1), DateTimeKind.Utc);
            dbQuery = dbQuery.Where(f => f.CreatedAt < nextDayUtc);
        }

        if (query.UpdatedAfter.HasValue)
        {
            var dateUtc = DateTime.SpecifyKind(query.UpdatedAfter.Value.Date, DateTimeKind.Utc);
            dbQuery = dbQuery.Where(f => f.UpdatedAt >= dateUtc);
        }

        if (query.UpdatedBefore.HasValue)
        {
            var nextDayUtc = DateTime.SpecifyKind(query.UpdatedBefore.Value.Date.AddDays(1), DateTimeKind.Utc);
            dbQuery = dbQuery.Where(f => f.UpdatedAt < nextDayUtc);
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

        var totalCount = await dbQuery.CountAsync(ct);

        var items = await dbQuery
            .AsNoTracking()
            .Skip(query.CurrentPage * query.PageSize)
            .Take(query.PageSize)
            .Select(FileProjections.ToFileResult)
            .ToListAsync(ct);

        return new PaginatedResult<FileResult>
        {
            Items = items,
            TotalCount = totalCount,
            CurrentPage = query.CurrentPage,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
        };
    }

    public async Task MoveFilesAsync(
        Guid[] fileIds,
        Guid? destinationId,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            var affected = await _files
                .Where(f =>
                    fileIds.Contains(f.Id) &&
                    f.OwnerId == userId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(f => f.DirectoryId, destinationId),
                    ct);

            if (affected != fileIds.Length)
                throw new InvalidOperationException("Some files were not found or not owned by user");
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                "Files with the same names already exist in destination",
                ex);
        }
    }

    public async Task<IEnumerable<File>> GetFilesByIds(Guid[] fileIds, CancellationToken ct = default)
    {
        return await _files
            .AsNoTracking()
            .Where(f => fileIds.Contains(f.Id))
            .ToListAsync(ct);
    }

    public async Task CopyFilesAsync(
        Guid[] fileIds,
        Guid destinationId,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            // 1. Load only what we need
            var sourceFiles = await _files
                .Where(f => fileIds.Contains(f.Id) && f.OwnerId == userId)
                .Select(f => new
                {
                    f.Name,
                    f.MimeType,
                    f.HasPreview,
                    f.PreviewGeneratedAt,
                    f.Tags,
                    f.PreviewId,
                    Version = f.CurrentVersion!
                })
                .ToListAsync(ct);

            if (sourceFiles.Count != fileIds.Length)
                throw new InvalidOperationException("Some files were not found or not owned by user");

            // 2. Create new entities in memory
            var now = DateTime.UtcNow;

            var newFiles = new List<File>(sourceFiles.Count);
            var newVersions = new List<FileVersion>(sourceFiles.Count);

            foreach (var src in sourceFiles)
            {
                if (src.Version is null)
                    throw new InvalidOperationException("Source file has no current version");

                var fileId = Guid.NewGuid();
                var versionId = Guid.NewGuid();

                newFiles.Add(new File
                {
                    Id = fileId,
                    Name = src.Name,
                    MimeType = src.MimeType,
                    CreatedAt = now,
                    HasPreview = src.HasPreview,
                    PreviewGeneratedAt = src.PreviewGeneratedAt,
                    Tags = src.Tags,
                    PreviewId = src.PreviewId,
                    OwnerId = userId,
                    DirectoryId = destinationId,
                    CurrentVersionId = versionId,
                    UpdatedBy = userId
                });

                newVersions.Add(new FileVersion
                {
                    Id = versionId,
                    FileId = fileId,
                    ContentHash = src.Version.ContentHash,
                    Size = src.Version.Size,
                    VersionNumber = 1,
                    MimeType = src.Version.MimeType,
                    CreatedAt = now,
                    CreatedBy = userId,
                    ContentObjectId = src.Version.ContentObjectId
                });
            }

            // 3. Insert in bulk
            _files.AddRange(newFiles);
            _fileVersions.AddRange(newVersions);

            await context.SaveChangesAsync(ct);

            // 4. Update ContentObject ref counts (grouped, set-based)
            var refCountDeltas = newVersions
                .GroupBy(v => v.ContentObjectId)
                .Select(g => new { ContentObjectId = g.Key, Count = g.Count() })
                .ToList();

            foreach (var delta in refCountDeltas)
            {
                await context.ContentObjects
                    .Where(co => co.Id == delta.ContentObjectId)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(
                            co => co.RefCount,
                            co => co.RefCount + delta.Count),
                        ct);
            }
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                "Files with the same names already exist in destination",
                ex);
        }
    }


    public async Task<File?> FirstOrDefaultAsync(Expression<Func<File, bool>> predicate, CancellationToken ct = default)
    {
        return await _files
            .Where(f => f.DeletedAt == null)
            .FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<FileMetadata?> GetUserFileMetadataAsync(Guid fileId, Guid userId, CancellationToken ct = default)
    {
        return await _files
            .Where(f => f.Id == fileId && f.OwnerId == userId)
            .Select(f => new FileMetadata
            {
                Id = f.Id,
                MimeTYpe = f.MimeType,
                FileName = f.Name,
                ContentHash = f.CurrentVersion.ContentHash,
                VersionId = f.CurrentVersion.Id
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<File>> GetAllAsync(CancellationToken ct = default)
    {
        return await _files
            .Where(f => f.DeletedAt == null)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<File>> FindAsync(Expression<Func<File, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _files
            .Where(f => f.DeletedAt == null)
            .Where(predicate)
            .ToListAsync(ct);
    }

    public async Task<File> AddAsync(File entity, CancellationToken ct = default)
    {
        // Set audit fields
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = null;
        entity.DeletedAt = null;

        var result = await _files.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<IEnumerable<File>> AddRangeAsync(IEnumerable<File> entities, CancellationToken ct = default)
    {
        var files = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var file in files)
        {
            file.CreatedAt = now;
            file.UpdatedAt = null;
            file.DeletedAt = null;
        }

        await _files.AddRangeAsync(files, ct);
        await context.SaveChangesAsync(ct);
        return files;
    }

    public void Update(File entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _files.Update(entity);
    }

    public void Remove(File entity)
    {
        // Implement soft delete
        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _files.Update(entity);
        // Note: SaveChanges should be called by the Unit of Work or service layer
    }

    public void RemoveRange(IEnumerable<File> entities)
    {
        var now = DateTime.UtcNow;
        var enumerable = entities as File[] ?? entities.ToArray();
        foreach (var entity in enumerable)
        {
            entity.DeletedAt = now;
            entity.UpdatedAt = now;
        }

        _files.UpdateRange(enumerable);
    }

    public async Task<int> CountAsync(Expression<Func<File, bool>>? predicate = null, CancellationToken ct = default)
    {
        var query = _files.Where(f => f.DeletedAt == null);

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        return await query.CountAsync(ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<File, bool>> predicate, CancellationToken ct = default)
    {
        return await _files
            .Where(f => f.DeletedAt == null)
            .AnyAsync(predicate, ct);
    }

    public async Task<File> CreateAsync(File file, CancellationToken ct = default)
    {
        return await AddAsync(file, ct);
    }

    public async Task<byte[]?> GetFileHash(Guid fileId, Guid ownerId, CancellationToken ct = default)
    {
        var result = await _files
            .AsNoTracking()
            .Select(f => new { f.Id, f.OwnerId, f.CurrentVersion.ContentHash })
            .FirstOrDefaultAsync(f => f.Id == fileId && f.OwnerId == ownerId, cancellationToken: ct);
        return result?.ContentHash;
    }

    public async Task<string> GetFileHashAsString(Guid fileId, Guid ownerId, CancellationToken ct = default)
    {
        var result = await _files
            .AsNoTracking()
            .Select(f => new { f.Id, f.OwnerId, f.CurrentVersion.ContentHash })
            .FirstOrDefaultAsync(f => f.Id == fileId && f.OwnerId == ownerId, cancellationToken: ct);

        if (result is null) throw new InvalidOperationException("File hash not found");

        return BitConverter.ToString(result.ContentHash)
            .Replace("-", "")
            .ToLowerInvariant();
        ;
    }

    public async Task<PaginatedResult<FileResult>> GetFilesByDirectoryIdAsync(
        Guid parentDirectoryId,
        Guid userId,
        int currentPage = 1,
        int pageSize = 25,
        SortDirection sortDirection = SortDirection.Asc,
        SortBy sortBy = SortBy.Name,
        CancellationToken ct = default)
    {
        var baseQuery = context.Files
            .AsNoTracking()
            .Where(f => f.DirectoryId == parentDirectoryId && f.OwnerId == userId && f.DeletedAt == null);

        // Apply sorting
        var sortedQuery = sortBy switch
        {
            SortBy.Name => sortDirection == SortDirection.Asc
                ? baseQuery.OrderBy(f => f.Name)
                : baseQuery.OrderByDescending(f => f.Name),
            SortBy.CreatedAt => sortDirection == SortDirection.Asc
                ? baseQuery.OrderBy(f => f.CreatedAt)
                : baseQuery.OrderByDescending(f => f.CreatedAt),
            SortBy.UpdatedAt => sortDirection == SortDirection.Asc
                ? baseQuery.OrderBy(f => f.UpdatedAt)
                : baseQuery.OrderByDescending(f => f.UpdatedAt),
            _ => baseQuery.OrderBy(f => f.Name)
        };

        var count = await baseQuery.CountAsync(ct);

        var itemsTask = sortedQuery
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .Select(FileProjections.ToFileResult)
            .ToListAsync(ct);


        return new PaginatedResult<FileResult>
        {
            Items = itemsTask.Result,
            CurrentPage = currentPage,
            PageSize = pageSize,
            TotalCount = count,
            TotalPages = (int)Math.Ceiling(count / (double)pageSize)
        };
    }

    public async Task<File> UpdateAsync(File file, CancellationToken ct = default)
    {
        var existingFile = await GetByIdAsync(file.Id, ct);
        if (existingFile == null)
        {
            throw new InvalidOperationException($"File with ID {file.Id} not found or has been deleted.");
        }

        // Update mutable properties
        existingFile.Name = file.Name;
        existingFile.HasPreview = file.HasPreview;
        existingFile.PreviewGeneratedAt = file.PreviewGeneratedAt;
        existingFile.UpdatedBy = file.UpdatedBy;
        existingFile.UpdatedAt = DateTime.UtcNow;

        // Note: MimeType is marked as init-only, so it shouldn't be updated

        _files.Update(existingFile);
        await context.SaveChangesAsync(ct);

        return existingFile;
    }

    public async Task<FileResult?> GetFileWithTagsAsync(
        Guid fileId,
        CancellationToken ct = default)
    {
        return await _files
            .AsNoTracking()
            .Where(f => f.Id == fileId && f.DeletedAt == null)
            .Select(FileProjections.ToFileResult)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<File?> GetFileEntityWithTagsAsync(
        Guid fileId,
        CancellationToken ct = default)
    {
        return await _files
            .Include(f => f.Tags)
            .Where(f => f.Id == fileId && f.DeletedAt == null)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<FileSummary?> GetFileNameAndMimeType(Guid fileId, CancellationToken ct = default)
    {
        return await _files
            .Where(f => f.Id == fileId)
            .Select(f => new FileSummary(f.Id, f.Name, f.MimeType, f.HasPreview))
            .FirstOrDefaultAsync(ct);
    }
}