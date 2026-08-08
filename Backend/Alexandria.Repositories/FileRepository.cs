using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Dto.Tags;
using Alexandria.Repositories.Projections;
using Microsoft.EntityFrameworkCore;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Repositories;

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
            .Include(f => f.FileTags)
            .ThenInclude(ft => ft.Tag)
            .Where(f => f.DeletedAt == null);

        filesQuery = query.MatchType switch
        {
            TagMatchType.Any => filesQuery.Where(f =>
                f.FileTags!.Any(ft =>
                    query.TagIds.Contains(ft.TagId) &&
                    ft.Source != TagSource.Suppressed &&
                    ft.Tag.DeletedAt == null)),

            TagMatchType.All => ApplyAllTagsFilter(filesQuery, query.TagIds),

            TagMatchType.Exact => filesQuery.Where(f =>
                f.FileTags!.Count(ft => ft.Source != TagSource.Suppressed && ft.Tag.DeletedAt == null) ==
                query.TagIds.Count &&
                f.FileTags!.Count(ft =>
                    query.TagIds.Contains(ft.TagId) &&
                    ft.Source != TagSource.Suppressed &&
                    ft.Tag.DeletedAt == null) == query.TagIds.Count),

            _ => throw new UnreachableException($"Unhandled match type: {query.MatchType}")
        };

        if (query.UserId.HasValue)
            filesQuery = filesQuery.Where(f =>
                f.FileTags!.Any(ft =>
                    ft.Tag.OwnerId == query.UserId.Value &&
                    ft.Source != TagSource.Suppressed &&
                    ft.Tag.DeletedAt == null));

        if (!string.IsNullOrWhiteSpace(query.MimeTypePrefix))
            filesQuery = filesQuery.Where(f => f.MimeType.StartsWith(query.MimeTypePrefix));

        if (query.CreatedAfter.HasValue) filesQuery = filesQuery.Where(f => f.CreatedAt >= query.CreatedAfter.Value);
        if (query.CreatedBefore.HasValue) filesQuery = filesQuery.Where(f => f.CreatedAt <= query.CreatedBefore.Value);

        var totalCount = await filesQuery.CountAsync(ct);

        var files = await filesQuery
            .OrderByDescending(f => f.CreatedAt)
            .Take(query.PageSize)
            .Skip(query.CurrentPage * query.PageSize)
            .Select(FileProjections.ToFileResult)
            .ToListAsync(ct);

        return new PaginatedResult<FileResult>
        {
            Items = [.. files],
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
            var currentTagId = tagId;
            query = query.Where(f =>
                f.FileTags!.Any(ft =>
                    ft.TagId == currentTagId &&
                    ft.Source != TagSource.Suppressed &&
                    ft.Tag.DeletedAt == null));
        }

        return query;
    }

    public async Task<int> MarkAsDeletedAsync(Guid[] fileIds, Guid userId, CancellationToken ct = default)
    {
        return await _files
            .Where(f => f.OwnerId == userId && fileIds.Contains(f.Id) && f.DeletedAt == null)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(f => f.DeletedAt, _ => DateTime.UtcNow)
                    .SetProperty(f => f.UpdatedBy, _ => userId),
                ct);
    }

    public async Task<bool> IsPromotedAsync(Guid fileId, CancellationToken ct = default)
    {
        return await _files.Where(f => f.Id == fileId)
            .Select(f => f.CurrentVersion.ContentObject.IsPromoted)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaginatedResult<FileResult>> FindFilesAsync(FileSearchQuery query, Guid userId,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(query.CurrentPage);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(query.PageSize, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.PageSize, 100);

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
            dbQuery = dbQuery.Where(f => f.DeletedAt != null);
        else
            dbQuery = dbQuery.Where(f => f.DeletedAt == null);

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
        if (query.DirectoryId.HasValue) dbQuery = dbQuery.Where(f => f.Id == query.DirectoryId.Value);

        if (query.ParentDirectoryId.HasValue)
            dbQuery = dbQuery.Where(f => f.DirectoryId == query.ParentDirectoryId.Value);

        if (query.OwnerId.HasValue) dbQuery = dbQuery.Where(f => f.OwnerId == query.OwnerId.Value);

        // File-specific filters
        if (query.MinSize.HasValue) dbQuery = dbQuery.Where(f => f.CurrentVersion.Size >= query.MinSize.Value);

        if (query.MaxSize.HasValue) dbQuery = dbQuery.Where(f => f.CurrentVersion.Size <= query.MaxSize.Value);

        if (!string.IsNullOrWhiteSpace(query.MimeType))
            dbQuery = dbQuery.Where(f => f.MimeType == query.MimeType.Trim());

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
        CancellationToken ct = default)
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

    public async Task CopyFilesAsync(
        Guid[] fileIds,
        Guid? destinationId,
        Guid userId,
        CancellationToken ct)
    {
        var existingTransaction = context.Database.CurrentTransaction;
        await using var transaction = existingTransaction is null
            ? await context.Database.BeginTransactionAsync(ct)
            : null;

        try
        {
            // 1. Load only what we need
            var sourceFiles = await _files
                .Where(f => fileIds.Contains(f.Id) && f.OwnerId == userId)
                .Select(f => new
                {
                    f.Name,
                    f.MimeType,
                    TagAssignments = f.FileTags!.Select(ft => new { ft.TagId, ft.Source, ft.Confidence }).ToList(),
                    Version = f.CurrentVersion!
                })
                .ToListAsync(ct);

            if (sourceFiles.Count != fileIds.Length)
                throw new InvalidOperationException("Some files were not found or not owned by user");

            var now = DateTime.UtcNow;
            var newFiles = new List<File>(sourceFiles.Count);
            var newVersions = new List<FileVersion>(sourceFiles.Count);
            var newFileTags = new List<FileTag>();

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
                    OwnerId = userId,
                    DirectoryId = destinationId,
                    CurrentVersionId = null,
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

                newFileTags.AddRange(src.TagAssignments.Select(ta => new FileTag
                {
                    Id = Guid.NewGuid(),
                    FileId = fileId,
                    TagId = ta.TagId,
                    Source = ta.Source,
                    Confidence = ta.Confidence,
                    CreatedAt = now
                }));
            }

            _files.AddRange(newFiles);
            _fileVersions.AddRange(newVersions);
            context.FileTags.AddRange(newFileTags);
            await context.SaveChangesAsync(ct);

            // 4. Wire up CurrentVersionId and update
            var versionLookup = newVersions.ToDictionary(v => v.FileId, v => v.Id);
            foreach (var file in newFiles)
                file.CurrentVersionId = versionLookup[file.Id];

            await context.SaveChangesAsync(ct);

            // Only commit if WE opened the transaction
            if (transaction is not null)
                await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(ct);
            throw new InvalidOperationException(
                "Files with the same names already exist in destination",
                ex);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(ct);
            throw;
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
        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _files.Update(entity);
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

        if (predicate != null) query = query.Where(predicate);

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

    public async Task<string?> GetMimeTypeByVersionIdAsync(Guid versionId, CancellationToken ct = default) =>
        await _files.Where(f => f.Versions.Any(v => v.Id == versionId))
            .Select(f => f.MimeType)
            .FirstOrDefaultAsync(ct);

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
        var existingFile = await GetByIdAsync(file.Id, ct) ??
                           throw new InvalidOperationException(
                               $"File with ID {file.Id} not found or has been deleted.");

        // Update mutable properties
        existingFile.Name = file.Name;
        existingFile.UpdatedBy = file.UpdatedBy;
        existingFile.UpdatedAt = DateTime.UtcNow;

        // Note: MimeType is marked as init-only, so it shouldn't be updated

        _files.Update(existingFile);
        await context.SaveChangesAsync(ct);

        return existingFile;
    }

    public async Task<FileResult?> GetFileWithTagsAsync(Guid userId, Guid fileId,
        CancellationToken ct = default)
    {
        return await _files
            .AsNoTracking()
            .Where(f => f.OwnerId == userId && f.Id == fileId && f.DeletedAt == null)
            .Select(FileProjections.ToFileResult)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<string?> VersionBelongsToUserAsync(Guid versionId, Guid userId, CancellationToken ct = default)
    {
        var hash = await context.FileVersions
            .AsNoTracking()
            .Where(v => v.Id == versionId && v.File.OwnerId == userId)
            .Select(v => (byte[]?)v.ContentHash)
            .FirstOrDefaultAsync(ct);

        return hash is null ? null : Convert.ToHexStringLower(hash);
    }

    public async Task<File?> GetFileEntityWithTagsAsync(
        Guid fileId,
        CancellationToken ct = default)
    {
        return await _files
            .Include(f => f.FileTags)
            .ThenInclude(ft => ft.Tag)
            .Where(f => f.Id == fileId && f.DeletedAt == null)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<long> GetDeletedSizeAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.FileVersions
            .Where(v => _files
                .Where(f => f.OwnerId == userId && f.DeletedAt != null)
                .Select(f => f.Id)
                .Contains(v.FileId))
            .SumAsync(v => v.Size, ct);
    }

    public async Task<IEnumerable<FileSummary>> GetOldFilesAsync(Guid userId, CancellationToken ct = default)
    {
        return await _files.Where(f =>
                f.OwnerId == userId && f.DeletedAt != null && f.DeletedAt < DateTime.UtcNow.AddDays(-30))
            .Select(f => new FileSummary(f.Id, f.Name, f.MimeType))
            .ToListAsync(ct);
    }

    public Task<Dictionary<string, long>> GetSizeByTypeAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return context.Files
            .Where(f => f.OwnerId == userId && f.DeletedAt == null)
            .Join(
                context.FileVersions,
                f => f.Id,
                v => v.FileId,
                (f, v) => new { f.MimeType, v.Size }
            )
            .GroupBy(x => x.MimeType)
            .Select(g => new
            {
                MimeType = g.Key,
                TotalSize = g.Sum(x => x.Size)
            })
            .ToDictionaryAsync(x => x.MimeType, x => x.TotalSize, ct);
    }

    public async Task<int> RestoreFilesAsync(Guid[] fileIds, Guid userId, CancellationToken ct = default)
    {
        var thresholdDate = DateTime.UtcNow.AddDays(-30);

        var files = await _files
            .Where(f =>
                fileIds.Contains(f.Id) &&
                f.OwnerId == userId &&
                f.DeletedAt != null &&
                f.DeletedAt > thresholdDate)
            .ToListAsync(ct);

        foreach (var file in files)
        {
            file.DeletedAt = null;
            file.UpdatedBy = userId;
            file.UpdatedAt = DateTime.UtcNow;
        }

        return files.Count;
    }

    public async Task<int> GetFileCountPerUserAsync(Guid userId, bool deletedOnly, CancellationToken ct = default)
    {
        var query = _files.Where(f => f.OwnerId == userId);

        if (deletedOnly)
            query = query.Where(f => f.DeletedAt != null);

        return await query.CountAsync(ct);
    }

    public async Task<long> GetStorageUsagePerUserAsync(Guid userId, bool onlyDeleted, CancellationToken ct = default)
    {
        return await _fileVersions
            .Where(v => v.File.OwnerId == userId)
            .Where(v => onlyDeleted ? v.File.DeletedAt != null : v.File.DeletedAt == null)
            .SumAsync(v => v.Size, ct);
    }

    public async Task ChangeActiveVersionAsync(Guid versionId, Guid fileId, Guid userId, CancellationToken ct = default)
    {
        _ = await _files.FirstOrDefaultAsync(
                f => f.Id == fileId && f.OwnerId == userId && f.Versions.Any(v => v.Id == versionId), ct) ??
            throw new InvalidOperationException("File with version not found");
        await _files.Where(f => f.Id == fileId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.CurrentVersionId, versionId), ct);
    }

    public async Task<FileResult?> GetFileWithOwnershipByIdAsync(Guid fileId, Guid userId,
        CancellationToken ct = default)
    {
        return await _files.Where(f => f.Id == fileId && f.OwnerId == userId).Select(FileProjections.ToFileResult)
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpdateCurrentVersionAsync(Guid fileId, Guid versionId, CancellationToken ct = default)
    {
        await _files
            .Where(f => f.Id == fileId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.CurrentVersionId, versionId)
                .SetProperty(f => f.UpdatedAt, DateTime.UtcNow), ct);
    }

    public async Task<Guid?> GetCurrentVersionIdAsync(Guid fileId, Guid userId, CancellationToken ct = default)
    {
        return await _files.Where(f => f.Id == fileId && f.OwnerId == userId).Select(f => f.CurrentVersionId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaginatedResult<MediaFileDto>> GetFilesForStreamingAsync(
        Guid userId, int page, int pageSize, string? query = null, Guid? playlistId = null, bool isVideo = false,
        CancellationToken ct = default)
    {
        var viableVersionIds = context.Set<TranspilationJob>()
            .Where(j =>
                j.UserId == userId
                && j.DeletedAt == null
                && j.Status == TranspilationStatus.Ready
                && j.IsVideo == isVideo
                && j.Representations.Any(r =>
                    r.DeletedAt == null
                    && r.Status == RepresentationStatus.Ready))
            .Select(j => j.VersionId);

        // Resolve playlist order before the main query.
        // The join walks: PlaylistItem -> TranspilationJob -> FileVersion -> File.
        List<Guid>? playlistFileIds = null;
        if (playlistId.HasValue)
            playlistFileIds = await context.Set<PlaylistItem>()
                .Where(pi => pi.PlaylistId == playlistId.Value && pi.DeletedAt == null)
                .OrderBy(pi => pi.Position)
                .Join(context.Set<TranspilationJob>(),
                    pi => pi.TranspilationJobId,
                    tj => tj.Id,
                    (pi, tj) => tj.VersionId)
                .Join(context.Set<FileVersion>(),
                    vId => vId,
                    fv => fv.Id,
                    (_, fv) => fv.FileId)
                .ToListAsync(ct);

        var isSearching = !string.IsNullOrWhiteSpace(query);
        if (isSearching)
        {
            var searchTerm = query!.Trim().ToLower();
            var tsqueryTerm = string.Join(" & ",
                searchTerm
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => Regex.Replace(w, @"[^a-z0-9]", ""))
                    .Where(w => w.Length > 0)
                    .Select(w => $"{w}:*"));

            if (string.IsNullOrWhiteSpace(tsqueryTerm))
            {
                return new PaginatedResult<MediaFileDto>
                {
                    Items = [],
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = 0,
                    TotalPages = 0,
                };
            }

            var fileNamePattern = $"%{searchTerm}%";

            // Pre-build the streamable ID set for this user so we can:
            //   a) filter metadata results to only files with a ready transcode job
            //   b) avoid .First() throwing during projection for non-streamable files
            var streamableIds = (await _files
                    .Where(f =>
                        f.OwnerId == userId
                        && f.DeletedAt == null
                        && f.Versions.Any(v => v.DeletedAt == null && viableVersionIds.Contains(v.Id)))
                    .Select(f => f.Id)
                    .ToListAsync(ct))
                .ToHashSet();

            // Step 1: metadata search ordered by relevance.
            //
            // IMPORTANT: materialize with ToListAsync() BEFORE projecting to FileId.
            // Chaining .Select() onto FromSqlInterpolated() causes EF to wrap the raw
            // SQL in a subquery, and PostgreSQL silently discards ORDER BY in subqueries
            // without LIMIT. Materializing first executes the SQL as-is.
            var metadataRankedIds = (await context.Set<MediaMetadata>()
                    .FromSqlInterpolated($@"
                        SELECT *
                        FROM ""MediaMetadata""
                        WHERE
                            {searchTerm} <% ""NormalizedSearch""
                            OR ""SearchVector"" @@ to_tsquery('simple', {tsqueryTerm})
                            OR lower(""Title"") LIKE {fileNamePattern}
                        ORDER BY
                            CASE WHEN lower(""Title"") LIKE {fileNamePattern} THEN 8.0 ELSE 0.0 END +
                            word_similarity({searchTerm}, ""NormalizedSearch"") * 2.0 +
                            ts_rank(""SearchVector"", to_tsquery('simple', {tsqueryTerm})) DESC")
                    .AsNoTracking()
                    .ToListAsync(ct))
                .Select(m => m.FileId)
                .Where(id => streamableIds.Contains(id))
                .ToList();
            // Step 2: filename fallback for files with no metadata tags.
            var metadataIdSet = metadataRankedIds.ToHashSet();
            var filenameOnlyIds = await _files
                .Where(f =>
                    f.OwnerId == userId
                    && f.DeletedAt == null
                    && f.Versions.Any(v => v.DeletedAt == null && viableVersionIds.Contains(v.Id))
                    && EF.Functions.ILike(f.Name, fileNamePattern))
                .Select(f => f.Id)
                .ToListAsync(ct);

            // Metadata matches first (ranked), filename-only matches appended.
            var orderedSearchIds = metadataRankedIds
                .Concat(filenameOnlyIds.Except(metadataIdSet))
                .ToList();

            // Step 3: narrow to playlist scope if active.
            if (playlistFileIds != null)
            {
                var playlistSet = playlistFileIds.ToHashSet();
                orderedSearchIds = orderedSearchIds.Where(playlistSet.Contains).ToList();
            }

            // Step 4: paginate on the ordered ID list.
            var totalCount = orderedSearchIds.Count;
            var pageIds = orderedSearchIds.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            if (pageIds.Count == 0)
                return new PaginatedResult<MediaFileDto>
                {
                    Items = [],
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

            // Step 5: fetch and project only this page's items.
            var searchItems = await _files
                .Where(f =>
                    f.OwnerId == userId
                    && f.DeletedAt == null
                    && pageIds.Contains(f.Id))
                .Select(f => new MediaFileDto
                {
                    FileId = f.Id,
                    FileName = f.Name,
                    MimeType = f.MimeType,
                    CurrentVersionId = f.CurrentVersion.Id,
                    Duration = f.MediaMetadata!.Duration,
                    Artist = f.MediaMetadata.Artist,
                    Album = f.MediaMetadata.Album,
                    Title = f.MediaMetadata.Title,
                    TranspilationJobId = context.Set<TranspilationJob>()
                        .Where(j => j.UserId == userId && j.Status == TranspilationStatus.Ready && j.DeletedAt == null
                                    && f.Versions.Any(v => v.DeletedAt == null && v.Id == j.VersionId))
                        .Select(j => j.Id)
                        .First(),
                    IsVideo = context.Set<TranspilationJob>()
                        .Where(j => j.UserId == userId && j.Status == TranspilationStatus.Ready && j.DeletedAt == null
                                    && f.Versions.Any(v => v.DeletedAt == null && v.Id == j.VersionId))
                        .Select(j => j.IsVideo)
                        .First(),
                    SegmentPrefix = context.Set<TranspilationJob>()
                        .Where(j => j.UserId == userId && j.Status == TranspilationStatus.Ready && j.DeletedAt == null
                                    && f.Versions.Any(v => v.DeletedAt == null && v.Id == j.VersionId))
                        .Select(j => j.SegmentPrefix)
                        .First()
                })
                .ToListAsync(ct);

            // IN (...) does not preserve list order, restore relevance ordering.
            var rankMap = pageIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            searchItems = searchItems
                .OrderBy(m => rankMap.GetValueOrDefault(m.FileId, int.MaxValue))
                .ToList();

            return new PaginatedResult<MediaFileDto>
            {
                Items = searchItems,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        var dbQuery = _files
            .Where(f =>
                f.OwnerId == userId
                && f.DeletedAt == null
                && f.Versions.Any(v =>
                    v.DeletedAt == null
                    && viableVersionIds.Contains(v.Id)));

        // Only apply CreatedAt ordering when not in playlist mode.
        // Playlist ordering is applied in memory after materialization.
        if (playlistFileIds == null)
            dbQuery = dbQuery.OrderByDescending(f => f.CreatedAt);

        if (playlistFileIds != null)
            dbQuery = dbQuery.Where(f => playlistFileIds.Contains(f.Id));

        var count = await dbQuery.CountAsync(ct);
        var items = await dbQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new MediaFileDto
            {
                FileId = f.Id,
                FileName = f.Name,
                MimeType = f.MimeType,
                CurrentVersionId = f.CurrentVersion.Id,
                Duration = f.MediaMetadata!.Duration,
                Artist = f.MediaMetadata.Artist,
                Album = f.MediaMetadata.Album,
                Title = f.MediaMetadata.Title,
                TranspilationJobId = context.Set<TranspilationJob>()
                    .Where(j => j.UserId == userId && j.Status == TranspilationStatus.Ready && j.DeletedAt == null
                                && f.Versions.Any(v => v.DeletedAt == null && v.Id == j.VersionId))
                    .Select(j => j.Id)
                    .First(),
                IsVideo = context.Set<TranspilationJob>()
                    .Where(j => j.UserId == userId && j.Status == TranspilationStatus.Ready && j.DeletedAt == null
                                && f.Versions.Any(v => v.DeletedAt == null && v.Id == j.VersionId))
                    .Select(j => j.IsVideo)
                    .First(),
                SegmentPrefix = context.Set<TranspilationJob>()
                    .Where(j => j.UserId == userId && j.Status == TranspilationStatus.Ready && j.DeletedAt == null
                                && f.Versions.Any(v => v.DeletedAt == null && v.Id == j.VersionId))
                    .Select(j => j.SegmentPrefix)
                    .First()
            })
            .ToListAsync(ct);

        if (playlistFileIds != null)
        {
            var positionMap = playlistFileIds
                .Select((id, i) => (id, i))
                .ToDictionary(x => x.id, x => x.i);

            items = items
                .OrderBy(m => positionMap.GetValueOrDefault(m.FileId, int.MaxValue))
                .ToList();
        }

        return new PaginatedResult<MediaFileDto>
        {
            Items = items,
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = count,
            TotalPages = (int)Math.Ceiling(count / (double)pageSize)
        };
    }

    public async Task<(DownloadMetadata fileMetadata, byte[] fileHash)?> GetDownloadMetadataAsync(
        Guid fileId, Guid userId, CancellationToken ct = default)
    {
        var row = await _files
            .Where(f => f.Id == fileId && f.OwnerId == userId && f.DeletedAt == null)
            .Select(f => new
            {
                f.Name,
                f.MimeType,
                f.CurrentVersion.EncryptionHint,
                f.CurrentVersion.EncryptionIv,
                f.CurrentVersion.IsEncrypted,
                f.CurrentVersion.EncryptionSalt,
                f.CurrentVersion.IntegrityTag,
                f.CurrentVersion.ContentHash
            })
            .FirstOrDefaultAsync(ct);

        if (row is null) return null;

        return (new DownloadMetadata
        {
            FileName = row.Name,
            MimeType = row.MimeType,
            EncryptionHint = row.EncryptionHint,
            EncryptionIv = row.EncryptionIv,
            IsEncrypted = row.IsEncrypted,
            EncryptionSalt = row.EncryptionSalt,
            IntegrityTag = row.IntegrityTag
        }, row.ContentHash);
    }
}