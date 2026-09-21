using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Alexandria.Common.Exceptions.Playlist;
using Alexandria.Common.Exceptions.Streaming;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Dto.Files.Streaming.Playlist;
using Alexandria.Dto.Files.Streaming.Shuffle;
using Alexandria.Dto.Tags;
using Alexandria.Repositories.Projections;
using Microsoft.EntityFrameworkCore;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Repositories;

public class FileRepository(AlexandriaDbContext context) : IFileRepository
{
    private readonly DbSet<FileVersion> _fileVersions = context.FileVersions;
    private readonly DbSet<File> _files = context.Files;

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
                f.FileTags.Any(ft =>
                    query.TagIds.Contains(ft.TagId) &&
                    ft.Source != TagSource.Suppressed &&
                    ft.Tag.DeletedAt == null)),

            TagMatchType.All => ApplyAllTagsFilter(filesQuery, query.TagIds),

            TagMatchType.Exact => filesQuery.Where(f =>
                f.FileTags.Count(ft => ft.Source != TagSource.Suppressed && ft.Tag.DeletedAt == null) ==
                query.TagIds.Count &&
                f.FileTags.Count(ft =>
                    query.TagIds.Contains(ft.TagId) &&
                    ft.Source != TagSource.Suppressed &&
                    ft.Tag.DeletedAt == null) == query.TagIds.Count),

            _ => throw new UnreachableException($"Unhandled match type: {query.MatchType}")
        };

        if (query.UserId.HasValue)
            filesQuery = filesQuery.Where(f =>
                f.FileTags.Any(ft =>
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

    public async Task<IReadOnlyList<AutoPlaylistGroupingRow>> GetAutoPlaylistGroupingRowsAsync(
        Guid ownerId, CancellationToken ct = default)
    {
        return await _files
            .AsNoTracking()
            .Where(f => f.OwnerId == ownerId && f.DeletedAt == null)
            .Select(f => new AutoPlaylistGroupingRow(
                f.Id,
                f.MediaMetadata!.Artist,
                f.MediaMetadata.Album,
                f.MediaMetadata.Genre,
                f.MediaMetadata.Year,
                f.FileTags
                    .Select(ft => new GroupingTagRef(
                        ft.TagId,
                        ft.Source,
                        ft.Tag.Facet,
                        ft.Tag.DeletedAt != null,
                        ft.Tag.ParentId,
                        ft.Tag.Parent!.Name))
                    .ToList()))
            .ToListAsync(ct);
    }

    public async Task<int> MarkAsDeletedAsync(Guid[] fileIds, Guid userId, CancellationToken ct = default)
    {
        return await _files
            .Where(f => f.OwnerId == userId && fileIds.AsEnumerable().Contains(f.Id) && f.DeletedAt == null)
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
                    fileIds.AsEnumerable().Contains(f.Id) &&
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
                .Where(f => fileIds.AsEnumerable().Contains(f.Id) && f.OwnerId == userId)
                .Select(f => new
                {
                    f.Name,
                    f.MimeType,
                    TagAssignments = f.FileTags.Select(ft => new { ft.TagId, ft.Source, ft.Confidence }).ToList(),
                    Version = f.CurrentVersion
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

    public async Task<string?> GetMimeTypeByVersionIdAsync(Guid versionId, CancellationToken ct = default)
    {
        return await _files.Where(f => f.Versions.Any(v => v.Id == versionId))
            .Select(f => f.MimeType)
            .FirstOrDefaultAsync(ct);
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
        var existingFile = await GetByIdAsync(file.Id, ct) ??
                           throw new InvalidOperationException(
                               $"File with ID {file.Id} not found or has been deleted.");

        // Update mutable properties
        existingFile.Name = file.Name;
        existingFile.UpdatedBy = file.UpdatedBy;
        existingFile.UpdatedAt = DateTime.UtcNow;

        // Note: MimeType is marked as init-only, so it shouldn't be updated

        _files.Update(existingFile);

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

    public async Task<Dictionary<Guid, long>> GetLiveSizeByOwnerAsync(CancellationToken ct = default)
    {
        return await context.Files
            .Where(f => f.DeletedAt == null)
            .Join(
                context.FileVersions,
                f => f.Id,
                v => v.FileId,
                (f, v) => new { f.OwnerId, v.Size }
            )
            .GroupBy(x => x.OwnerId)
            .Select(g => new
            {
                OwnerId = g.Key,
                TotalSize = g.Sum(x => x.Size)
            })
            .ToDictionaryAsync(x => x.OwnerId, x => x.TotalSize, ct);
    }

    public async Task<long> GetTotalTrashSizeAsync(CancellationToken ct = default)
    {
        return await context.FileVersions
            .Where(v => _files
                .Where(f => f.DeletedAt != null)
                .Select(f => f.Id)
                .Contains(v.FileId))
            .SumAsync(v => (long?)v.Size, ct) ?? 0;
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
                fileIds.AsEnumerable().Contains(f.Id) &&
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
        Guid? anchorFileId = null, Guid? anchorPlaylistItemId = null, CancellationToken ct = default)
    {
        if (playlistId.HasValue)
        {
            var playlistExists = await context.Playlists
                .AnyAsync(p => p.Id == playlistId.Value && p.OwnerId == userId && p.DeletedAt == null, ct);
            if (!playlistExists)
                throw new PlaylistNotFoundException(playlistId.Value);
        }

        // One round trip: every version with a Ready job (backed by Ready
        // representations) on this tab. Materialized once so the queries below
        // share an IN list instead of re-running the job join each time.
        var viableVersionIds = await StreamingSourceQueries.GetViableVersionIdsAsync(context, userId, isVideo, ct);

        if (viableVersionIds.Count == 0)
        {
            if (anchorFileId.HasValue || anchorPlaylistItemId.HasValue)
                throw new StreamingAnchorNotFoundException();
            return new PaginatedResult<MediaFileDto>
            {
                Items = [],
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = 0,
                TotalPages = 0
            };
        }

        List<Guid>? playlistFileIds = null;
        if (playlistId.HasValue)
            playlistFileIds = await context.PlaylistItems
                .Where(pi => pi.PlaylistId == playlistId.Value && pi.DeletedAt == null)
                .OrderBy(pi => pi.Position)
                .ThenBy(pi => pi.Id)
                .Join(context.TranspilationJobs,
                    pi => pi.TranspilationJobId,
                    tj => tj.Id,
                    (pi, tj) => tj.VersionId)
                .Join(context.FileVersions,
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
                return new PaginatedResult<MediaFileDto>
                {
                    Items = [],
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = 0,
                    TotalPages = 0
                };

            var fileNamePattern = $"%{searchTerm}%";

            var streamableIds = (await _files
                    .Where(f =>
                        f.OwnerId == userId
                        && f.DeletedAt == null
                        && f.Versions.Any(v => v.DeletedAt == null && viableVersionIds.Contains(v.Id)))
                    .Select(f => f.Id)
                    .ToListAsync(ct))
                .ToHashSet();

            var metadataRankedIds = (await context.MediaMetadata
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
                .Where(streamableIds.Contains)
                .ToList();

            var metadataIdSet = metadataRankedIds.ToHashSet();
            var filenameOnlyIds = await _files
                .Where(f =>
                    f.OwnerId == userId
                    && f.DeletedAt == null
                    && f.Versions.Any(v => v.DeletedAt == null && viableVersionIds.Contains(v.Id))
                    && EF.Functions.ILike(f.Name, fileNamePattern))
                .Select(f => f.Id)
                .ToListAsync(ct);

            var orderedSearchIds = metadataRankedIds
                .Concat(filenameOnlyIds.Except(metadataIdSet))
                .ToList();

            if (playlistFileIds != null)
            {
                var playlistSet = playlistFileIds.ToHashSet();
                orderedSearchIds = orderedSearchIds.Where(playlistSet.Contains).ToList();
            }

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

            var searchRows = await SelectStreamingRows(
                    StreamableFiles(userId, viableVersionIds).Where(f => pageIds.Contains(f.Id)),
                    userId, isVideo, viableVersionIds)
                .ToListAsync(ct);

            var rankMap = pageIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            var searchItems = searchRows
                .Where(r => r.Job != null)
                .OrderBy(r => rankMap.GetValueOrDefault(r.Id, int.MaxValue))
                .Select(ToMediaFileDto)
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

        if (playlistId.HasValue)
            return await GetPlaylistPageAsync(
                userId, playlistId.Value, isVideo, page, pageSize, viableVersionIds,
                anchorFileId, anchorPlaylistItemId, ct);

        var libraryQuery = StreamableFiles(userId, viableVersionIds)
            .OrderByDescending(f => f.CreatedAt)
            .ThenBy(f => f.Id);

        var effectivePage = page;
        if (anchorFileId.HasValue || anchorPlaylistItemId.HasValue)
        {
            if (anchorPlaylistItemId.HasValue || !anchorFileId.HasValue)
                throw new StreamingAnchorNotFoundException();
            effectivePage = await ResolveLibraryAnchorPageAsync(
                userId, viableVersionIds, anchorFileId.Value, pageSize, ct);
        }

        var libraryCount = await libraryQuery.CountAsync(ct);
        var libraryItems = (await SelectStreamingRows(
                    libraryQuery.Skip((effectivePage - 1) * pageSize).Take(pageSize),
                    userId, isVideo, viableVersionIds)
                .ToListAsync(ct))
            .Where(r => r.Job != null)
            .Select(ToMediaFileDto)
            .ToList();

        return new PaginatedResult<MediaFileDto>
        {
            Items = libraryItems,
            CurrentPage = effectivePage,
            PageSize = pageSize,
            TotalCount = libraryCount,
            TotalPages = (int)Math.Ceiling(libraryCount / (double)pageSize)
        };
    }

    private async Task<PaginatedResult<MediaFileDto>> GetPlaylistPageAsync(
        Guid userId, Guid playlistId, bool isVideo, int page, int pageSize,
        HashSet<Guid> viableVersionIds, Guid? anchorFileId, Guid? anchorPlaylistItemId,
        CancellationToken ct)
    {
        var eligible = StreamingSourceQueries.EligiblePlaylistItems(context, userId, playlistId, isVideo);

        var effectivePage = page;
        if (anchorFileId.HasValue || anchorPlaylistItemId.HasValue)
            effectivePage = await ResolvePlaylistAnchorPageAsync(
                eligible, anchorFileId, anchorPlaylistItemId, pageSize, ct);

        var totalCount = await eligible.CountAsync(ct);
        if (totalCount == 0)
            return new PaginatedResult<MediaFileDto>
            {
                Items = [],
                CurrentPage = effectivePage,
                PageSize = pageSize,
                TotalCount = 0,
                TotalPages = 0
            };

        var pageItems = await eligible
            .OrderBy(pi => pi.Position)
            .ThenBy(pi => pi.Id)
            .Skip((effectivePage - 1) * pageSize)
            .Take(pageSize)
            .Select(pi => new PlaylistStreamingRow
            {
                ItemId = pi.Id,
                Position = pi.Position,
                FileId = pi.TranspilationJob!.FileVersion.FileId,
                FileName = pi.TranspilationJob!.FileVersion.File.Name,
                MimeType = pi.TranspilationJob!.FileVersion.File.MimeType,
                CurrentVersionId = pi.TranspilationJob!.FileVersion.File.CurrentVersionId!.Value,
                PlaybackVersionId = pi.TranspilationJob.VersionId,
                Duration = pi.TranspilationJob!.FileVersion.File.MediaMetadata == null
                    ? null
                    : pi.TranspilationJob!.FileVersion.File.MediaMetadata.Duration,
                Artist = pi.TranspilationJob!.FileVersion.File.MediaMetadata!.Artist,
                Album = pi.TranspilationJob!.FileVersion.File.MediaMetadata.Album,
                Title = pi.TranspilationJob!.FileVersion.File.MediaMetadata.Title,
                Genre = pi.TranspilationJob!.FileVersion.File.MediaMetadata.Genre,
                Year = pi.TranspilationJob!.FileVersion.File.MediaMetadata.Year,
                JobId = pi.TranspilationJobId,
                IsVideo = pi.TranspilationJob!.IsVideo,
                SegmentPrefix = pi.TranspilationJob!.SegmentPrefix,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        return new PaginatedResult<MediaFileDto>
        {
            Items = pageItems.Select(ToMediaFileDto).ToList(),
            CurrentPage = effectivePage,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    private async Task<int> ResolveLibraryAnchorPageAsync(
        Guid userId, HashSet<Guid> viableVersionIds, Guid anchorFileId, int pageSize,
        CancellationToken ct)
    {
        var anchor = await StreamableFiles(userId, viableVersionIds)
            .Where(f => f.Id == anchorFileId)
            .Select(f => new { f.CreatedAt, f.Id })
            .FirstOrDefaultAsync(ct);
        if (anchor is null)
            throw new StreamingAnchorNotFoundException();

        var preceding = await StreamableFiles(userId, viableVersionIds)
            .CountAsync(f => f.CreatedAt > anchor.CreatedAt
                             || (f.CreatedAt == anchor.CreatedAt && f.Id.CompareTo(anchor.Id) < 0), ct);

        return preceding / pageSize + 1;
    }

    private static async Task<int> ResolvePlaylistAnchorPageAsync(
        IQueryable<PlaylistItem> eligible, Guid? anchorFileId, Guid? anchorPlaylistItemId,
        int pageSize, CancellationToken ct)
    {
        PlaylistAnchorKey anchor;
        if (anchorPlaylistItemId.HasValue)
        {
            if (!anchorFileId.HasValue)
                throw new StreamingAnchorNotFoundException();
            anchor = await eligible
                         .Where(pi => pi.Id == anchorPlaylistItemId.Value
                                      && pi.TranspilationJob!.FileVersion.FileId == anchorFileId.Value)
                         .Select(pi => new PlaylistAnchorKey(pi.Position, pi.Id))
                         .FirstOrDefaultAsync(ct)
                     ?? throw new StreamingAnchorNotFoundException();
        }
        else if (anchorFileId.HasValue)
        {
            anchor = await eligible
                         .Where(pi => pi.TranspilationJob!.FileVersion.FileId == anchorFileId.Value)
                         .OrderBy(pi => pi.Position)
                         .ThenBy(pi => pi.Id)
                         .Select(pi => new PlaylistAnchorKey(pi.Position, pi.Id))
                         .FirstOrDefaultAsync(ct)
                     ?? throw new StreamingAnchorNotFoundException();
        }
        else
        {
            throw new StreamingAnchorNotFoundException();
        }

        var preceding = await eligible
            .CountAsync(pi => pi.Position < anchor.Position
                              || (pi.Position == anchor.Position && pi.Id.CompareTo(anchor.ItemId) < 0), ct);

        return preceding / pageSize + 1;
    }


    // Files that can actually appear in the grid: owned, alive, with a pinned
    // current version and at least one viable version. The CurrentVersionId
    // check both skips transient upload states and keeps the projection below
    // from reading a non-nullable Guid over an empty LEFT JOIN (which throws
    // InvalidOperationException at materialization).
    private IQueryable<File> StreamableFiles(Guid userId, HashSet<Guid> viableVersionIds) =>
        _files.Where(f =>
            f.OwnerId == userId
            && f.DeletedAt == null
            && f.CurrentVersionId != null
            && f.Versions.Any(v => v.DeletedAt == null && viableVersionIds.Contains(v.Id)));

    // Single shared projection for both the search and plain paths. Metadata
    // is an optional LEFT JOIN: a missing row yields null Duration (unknown)
    // instead of crashing, which happens for files with no preview yet since
    // duration is produced by preview generation. The job lookup reuses the
    // viable set and prefers the current version's job so the returned job id
    // lines up with the thumbnail/manifest version.
    private IQueryable<StreamingFileRow> SelectStreamingRows(
        IQueryable<File> files, Guid userId, bool isVideo, HashSet<Guid> viableVersionIds) =>
        files.Select(f => new StreamingFileRow
        {
            Id = f.Id,
            Name = f.Name,
            MimeType = f.MimeType,
            CurrentVersionId = f.CurrentVersionId!.Value,
            Duration = f.MediaMetadata == null ? null : f.MediaMetadata.Duration,
            Artist = f.MediaMetadata!.Artist,
            Album = f.MediaMetadata.Album,
            Title = f.MediaMetadata.Title,
            Genre = f.MediaMetadata.Genre,
            Year = f.MediaMetadata.Year,
            Job = context.TranspilationJobs
                .Where(j => j.UserId == userId
                            && j.IsVideo == isVideo
                            && viableVersionIds.Contains(j.VersionId)
                            && f.Versions.Any(v => v.DeletedAt == null && v.Id == j.VersionId))
                .OrderByDescending(j => j.VersionId == f.CurrentVersionId)
                .ThenBy(j => j.Id)
                .Select(j => new StreamingJobRow
                {
                    Id = j.Id,
                    VersionId = j.VersionId,
                    IsVideo = j.IsVideo,
                    SegmentPrefix = j.SegmentPrefix
                })
                .FirstOrDefault()
        });

    private static MediaFileDto ToMediaFileDto(StreamingFileRow row) =>
        new MediaFileDto
        {
            FileId = row.Id,
            FileName = row.Name,
            MimeType = row.MimeType,
            CurrentVersionId = row.CurrentVersionId,
            PlaybackVersionId = row.Job!.VersionId,
            Duration = row.Duration,
            Artist = row.Artist,
            Album = row.Album,
            Title = row.Title,
            Genre = row.Genre,
            Year = row.Year,
            TranspilationJobId = row.Job!.Id,
            PlaylistItemId = null,
            IsVideo = row.Job.IsVideo,
            SegmentPrefix = row.Job.SegmentPrefix
        };

    private static MediaFileDto ToMediaFileDto(PlaylistStreamingRow row) =>
        new MediaFileDto
        {
            FileId = row.FileId,
            FileName = row.FileName,
            MimeType = row.MimeType,
            CurrentVersionId = row.CurrentVersionId,
            PlaybackVersionId = row.PlaybackVersionId,
            Duration = row.Duration,
            Artist = row.Artist,
            Album = row.Album,
            Title = row.Title,
            Genre = row.Genre,
            Year = row.Year,
            TranspilationJobId = row.JobId,
            PlaylistItemId = row.ItemId,
            IsVideo = row.IsVideo,
            SegmentPrefix = row.SegmentPrefix
        };

    internal IQueryable<ShuffleCandidate> ShuffleLibraryCandidatesQuery(
        Guid userId, bool isVideo, HashSet<Guid> viableVersionIds) =>
        StreamableFiles(userId, viableVersionIds)
            .OrderByDescending(f => f.CreatedAt)
            .ThenBy(f => f.Id)
            .Select(f => new ShuffleCandidate
            {
                Entry = new PlaybackSourceEntryRef
                {
                    FileId = f.Id,
                    PlaylistItemId = null,
                    TranspilationJobId = context.TranspilationJobs
                        .Where(j => j.UserId == userId
                                    && j.IsVideo == isVideo
                                    && viableVersionIds.Contains(j.VersionId)
                                    && f.Versions.Any(v => v.DeletedAt == null && v.Id == j.VersionId))
                        .OrderByDescending(j => j.VersionId == f.CurrentVersionId)
                        .ThenBy(j => j.Id)
                        .Select(j => j.Id)
                        .FirstOrDefault(),
                },
                DurationSeconds = f.MediaMetadata == null ? null : (double?)f.MediaMetadata.Duration,
            });

    internal IQueryable<ShuffleCandidate> ShufflePlaylistCandidatesQuery(
        Guid userId, Guid playlistId, bool isVideo) =>
        StreamingSourceQueries.EligiblePlaylistItems(context, userId, playlistId, isVideo)
            .OrderBy(pi => pi.Position)
            .ThenBy(pi => pi.Id)
            .Select(pi => new ShuffleCandidate
            {
                Entry = new PlaybackSourceEntryRef
                {
                    FileId = pi.TranspilationJob!.FileVersion.FileId,
                    PlaylistItemId = pi.Id,
                    TranspilationJobId = pi.TranspilationJobId,
                },
                DurationSeconds = pi.TranspilationJob!.FileVersion.File.MediaMetadata == null
                    ? null
                    : (double?)pi.TranspilationJob!.FileVersion.File.MediaMetadata.Duration,
            });

    public async Task<IReadOnlyList<ShuffleCandidate>> GetShuffleCandidatesAsync(
        Guid userId, PlaybackSourceDto source, CancellationToken ct = default)
    {
        if (source.PlaylistId.HasValue)
        {
            var playlistExists = await context.Playlists
                .AnyAsync(p => p.Id == source.PlaylistId.Value && p.OwnerId == userId && p.DeletedAt == null, ct);
            if (!playlistExists)
                return [];
            return await ShufflePlaylistCandidatesQuery(userId, source.PlaylistId.Value, source.IsVideo)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        var viableVersionIds =
            await StreamingSourceQueries.GetViableVersionIdsAsync(context, userId, source.IsVideo, ct);
        if (viableVersionIds.Count == 0)
            return [];
        return await ShuffleLibraryCandidatesQuery(userId, source.IsVideo, viableVersionIds)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MediaFileDto>> GetPlaybackEntriesAsync(
        Guid userId, PlaybackSourceDto source, IReadOnlyList<PlaybackSourceEntryRef> entryRefs,
        CancellationToken ct = default)
    {
        if (entryRefs.Count == 0)
            return [];

        if (source.PlaylistId.HasValue)
            return await GetPlaylistPlaybackEntriesAsync(userId, source, entryRefs, ct);

        return await GetLibraryPlaybackEntriesAsync(userId, source.IsVideo, entryRefs, ct);
    }

    private async Task<IReadOnlyList<MediaFileDto>> GetLibraryPlaybackEntriesAsync(
        Guid userId, bool isVideo, IReadOnlyList<PlaybackSourceEntryRef> entryRefs,
        CancellationToken ct)
    {
        var requestedIds = entryRefs.Select(r => r.FileId).Distinct().ToList();
        var rows = await _files
            .AsNoTracking()
            .Where(f => f.OwnerId == userId
                        && f.DeletedAt == null
                        && f.CurrentVersionId != null
                        && requestedIds.Contains(f.Id))
            .Select(f => new LibraryPlaybackRow
            {
                Id = f.Id,
                Name = f.Name,
                MimeType = f.MimeType,
                CurrentVersionId = f.CurrentVersionId!.Value,
                Duration = f.MediaMetadata == null ? null : (double?)f.MediaMetadata.Duration,
                Artist = f.MediaMetadata!.Artist,
                Album = f.MediaMetadata.Album,
                Title = f.MediaMetadata.Title,
                Genre = f.MediaMetadata.Genre,
                Year = f.MediaMetadata.Year,
                LiveVersionIds = f.Versions.Where(v => v.DeletedAt == null).Select(v => v.Id).ToList(),
            })
            .ToListAsync(ct);

        var fileRows = rows.ToDictionary(r => r.Id);
        if (fileRows.Count == 0)
            return [];

        var versionIds = fileRows.Values.SelectMany(r => r.LiveVersionIds).Distinct().ToList();
        var jobs = await context.TranspilationJobs
            .AsNoTracking()
            .Where(j => j.UserId == userId
                        && j.IsVideo == isVideo
                        && j.DeletedAt == null
                        && j.Job.DeletedAt == null
                        && j.Job.Status == JobStatus.Ready
                        && versionIds.Contains(j.VersionId)
                        && j.Representations.Any(r => r.DeletedAt == null && r.Status == RepresentationStatus.Ready))
            .Select(j => new { j.Id, j.VersionId, j.IsVideo, j.SegmentPrefix })
            .ToListAsync(ct);
        var jobsByVersion = new Dictionary<Guid, StreamingJobRow>();
        foreach (var job in jobs)
            jobsByVersion.TryAdd(job.VersionId, new StreamingJobRow
            {
                Id = job.Id,
                VersionId = job.VersionId,
                IsVideo = job.IsVideo,
                SegmentPrefix = job.SegmentPrefix,
            });

        var seen = new HashSet<Guid>();
        var ordered = new List<MediaFileDto>(entryRefs.Count);
        foreach (var entry in entryRefs)
        {
            if (!seen.Add(entry.FileId))
                continue;
            if (!fileRows.TryGetValue(entry.FileId, out var row))
                continue;

            var chosen = ResolveLibraryJob(row, jobsByVersion);
            if (chosen is null)
                continue;

            ordered.Add(new MediaFileDto
            {
                FileId = row.Id,
                FileName = row.Name,
                MimeType = row.MimeType,
                CurrentVersionId = row.CurrentVersionId,
                PlaybackVersionId = chosen.VersionId,
                Duration = row.Duration,
                Artist = row.Artist,
                Album = row.Album,
                Title = row.Title,
                Genre = row.Genre,
                Year = row.Year,
                TranspilationJobId = chosen.Id,
                PlaylistItemId = null,
                IsVideo = chosen.IsVideo,
                SegmentPrefix = chosen.SegmentPrefix,
            });
        }

        return ordered;
    }

    private static StreamingJobRow? ResolveLibraryJob(
        LibraryPlaybackRow row, Dictionary<Guid, StreamingJobRow> jobsByVersion)
    {
        if (row.LiveVersionIds.Contains(row.CurrentVersionId)
            && jobsByVersion.TryGetValue(row.CurrentVersionId, out var currentJob))
            return currentJob;

        foreach (var versionId in row.LiveVersionIds.OrderBy(v => v))
        {
            if (jobsByVersion.TryGetValue(versionId, out var fallback))
                return fallback;
        }

        return null;
    }

    private async Task<IReadOnlyList<MediaFileDto>> GetPlaylistPlaybackEntriesAsync(
        Guid userId, PlaybackSourceDto source, IReadOnlyList<PlaybackSourceEntryRef> entryRefs,
        CancellationToken ct)
    {
        var playlistId = source.PlaylistId!.Value;
        var playlistExists = await context.Playlists
            .AnyAsync(p => p.Id == playlistId && p.OwnerId == userId && p.DeletedAt == null, ct);
        if (!playlistExists)
            return [];

        var requestedItemIds = entryRefs
            .Where(r => r.PlaylistItemId.HasValue)
            .Select(r => r.PlaylistItemId!.Value)
            .Distinct()
            .ToList();
        if (requestedItemIds.Count == 0)
            return [];

        var rows = await StreamingSourceQueries.EligiblePlaylistItems(context, userId, playlistId, source.IsVideo)
            .Where(pi => requestedItemIds.Contains(pi.Id))
            .Select(pi => new PlaylistStreamingRow
            {
                ItemId = pi.Id,
                Position = pi.Position,
                FileId = pi.TranspilationJob!.FileVersion.FileId,
                FileName = pi.TranspilationJob!.FileVersion.File.Name,
                MimeType = pi.TranspilationJob!.FileVersion.File.MimeType,
                CurrentVersionId = pi.TranspilationJob!.FileVersion.File.CurrentVersionId!.Value,
                PlaybackVersionId = pi.TranspilationJob.VersionId,
                Duration = pi.TranspilationJob!.FileVersion.File.MediaMetadata == null
                    ? null
                    : (double?)pi.TranspilationJob!.FileVersion.File.MediaMetadata.Duration,
                Artist = pi.TranspilationJob!.FileVersion.File.MediaMetadata!.Artist,
                Album = pi.TranspilationJob!.FileVersion.File.MediaMetadata.Album,
                Title = pi.TranspilationJob!.FileVersion.File.MediaMetadata.Title,
                Genre = pi.TranspilationJob!.FileVersion.File.MediaMetadata.Genre,
                Year = pi.TranspilationJob!.FileVersion.File.MediaMetadata.Year,
                JobId = pi.TranspilationJobId,
                IsVideo = pi.TranspilationJob!.IsVideo,
                SegmentPrefix = pi.TranspilationJob!.SegmentPrefix,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var byItem = rows.ToDictionary(r => r.ItemId);
        var ordered = new List<MediaFileDto>(entryRefs.Count);

        foreach (var entry in entryRefs)
        {
            if (!entry.PlaylistItemId.HasValue)
                continue;
            if (byItem.TryGetValue(entry.PlaylistItemId.Value, out var row))
                ordered.Add(ToMediaFileDto(row));
        }

        return ordered;
    }

    public async Task<MediaFileDto?> GetStreamingFileAsync(
        Guid userId, Guid fileId, CancellationToken ct = default)
    {
        var file = await _files
            .Where(f => f.Id == fileId
                        && f.OwnerId == userId
                        && f.DeletedAt == null
                        && f.CurrentVersionId != null)
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.MimeType,
                CurrentVersionId = f.CurrentVersionId!.Value,
                Metadata = f.MediaMetadata,
                VersionIds = f.Versions
                    .Where(v => v.DeletedAt == null)
                    .Select(v => v.Id)
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (file is null || file.VersionIds.Count == 0) return null;

        var job = await context.TranspilationJobs
            .Where(j => j.UserId == userId
                        && j.DeletedAt == null
                        && j.Job.DeletedAt == null
                        && j.Job.Status == JobStatus.Ready
                        && file.VersionIds.Contains(j.VersionId)
                        && j.Representations.Any(r =>
                            r.DeletedAt == null
                            && r.Status == RepresentationStatus.Ready))
            .OrderByDescending(j => j.VersionId == file.CurrentVersionId)
            .ThenBy(j => j.Id)
            .Select(j => new StreamingJobRow
            {
                Id = j.Id,
                VersionId = j.VersionId,
                IsVideo = j.IsVideo,
                SegmentPrefix = j.SegmentPrefix
            })
            .FirstOrDefaultAsync(ct);

        if (job is null) return null;

        return new MediaFileDto
        {
            FileId = file.Id,
            FileName = file.Name,
            MimeType = file.MimeType,
            CurrentVersionId = file.CurrentVersionId,
            PlaybackVersionId = job.VersionId,
            Duration = file.Metadata == null ? null : file.Metadata.Duration,
            Artist = file.Metadata!.Artist,
            Album = file.Metadata.Album,
            Title = file.Metadata.Title,
            Genre = file.Metadata.Genre,
            Year = file.Metadata.Year,
            TranspilationJobId = job.Id,
            IsVideo = job.IsVideo,
            SegmentPrefix = job.SegmentPrefix
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
                f.CurrentVersion.IterationCount,
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
            IntegrityTag = row.IntegrityTag,
            IterationCount = row.IterationCount
        }, row.ContentHash);
    }

    private static IQueryable<File> ApplyAllTagsFilter(IQueryable<File> query, ICollection<Guid> tagIds)
    {
        foreach (var tagId in tagIds)
        {
            var currentTagId = tagId;
            query = query.Where(f =>
                f.FileTags.Any(ft =>
                    ft.TagId == currentTagId &&
                    ft.Source != TagSource.Suppressed &&
                    ft.Tag.DeletedAt == null));
        }

        return query;
    }

    public async Task<IEnumerable<File>> GetAllAsync(CancellationToken ct = default)
    {
        return await _files
            .Where(f => f.DeletedAt == null)
            .ToListAsync(ct);
    }
}