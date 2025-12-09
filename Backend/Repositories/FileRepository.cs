using System.Linq.Expressions;
using Common;
using Common.Repositories;
using Data.Context;
using DTO;
using DTO.Files;
using DTO.Tags;
using Microsoft.EntityFrameworkCore;
using Models.Enumerators;
using File = Models.File;

namespace Repositories;

public class FileRepository(AlexandriaDbContext context) : IFileRepository
{
    private readonly DbSet<File> _files = context.Files;

    public async Task<File?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _files
            .Where(f => f.DeletedAt == null)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
    }
    
    public async Task<(IEnumerable<File> Files, int TotalCount)> FindFilesByTagsAsync(
        FileTagSearchQuery query, 
        CancellationToken ct = default)
    {
        IQueryable<File> filesQuery = _files
            .Include(f => f.Tags)
            .Where(f => f.DeletedAt == null);

        // Apply tag filtering based on match type
        filesQuery = query.MatchType switch
        {
            // ANY: File has at least one of the specified tags
            TagMatchType.Any => filesQuery.Where(f => 
                f.Tags.Any(t => query.TagIds.Contains(t.Id) && t.DeletedAt == null)),
            
            // ALL: File has all of the specified tags
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

        // Apply file size filters
        if (query.MinFileSize.HasValue)
        {
            filesQuery = filesQuery.Where(f => f.Size >= query.MinFileSize.Value);
        }

        if (query.MaxFileSize.HasValue)
        {
            filesQuery = filesQuery.Where(f => f.Size <= query.MaxFileSize.Value);
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
            .ToListAsync(ct);

        return (files, totalCount);
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
    
    public async Task<File> GetFileWithPreviewAsync(Guid id, CancellationToken ct = default)
    {
        return await _files
            .Include(f => f.Preview)
            .Where(f => f.DeletedAt == null)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<File?> FirstOrDefaultAsync(Expression<Func<File, bool>> predicate, CancellationToken ct = default)
    {
        return await _files
            .Where(f => f.DeletedAt == null)
            .FirstOrDefaultAsync(predicate, ct);
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
        // Note: SaveChanges should be called by the Unit of Work or service layer
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
        foreach (var entity in entities)
        {
            entity.DeletedAt = now;
            entity.UpdatedAt = now;
        }

        _files.UpdateRange(entities);
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

    public async Task<File> UpdateAsync(File file, CancellationToken ct = default)
    {
        var existingFile = await GetByIdAsync(file.Id, ct);
        if (existingFile == null)
        {
            throw new InvalidOperationException($"File with ID {file.Id} not found or has been deleted.");
        }

        // Update mutable properties
        existingFile.Name = file.Name;
        existingFile.Path = file.Path;
        existingFile.Size = file.Size;
        existingFile.HasPreview = file.HasPreview;
        existingFile.PreviewGeneratedAt = file.PreviewGeneratedAt;
        existingFile.UpdatedBy = file.UpdatedBy;
        existingFile.UpdatedAt = DateTime.UtcNow;

        // Note: MimeType is marked as init-only, so it shouldn't be updated

        _files.Update(existingFile);
        await context.SaveChangesAsync(ct);

        return existingFile;
    }

    public async Task<File?> GetFileWithTagsAsync(Guid fileId, CancellationToken ct = default)
    {
        return await _files.Include(f => f.Tags)
            .Where(f => f.DeletedAt == null)
            .FirstOrDefaultAsync(f => f.Id == fileId, ct);
    }

    public async Task<FileSummary?> GetFileNameAndMimeType(Guid fileId, CancellationToken ct = default)
    {
        return await _files
            .Where(f => f.Id == fileId)
            .Select(f => new FileSummary(f.Id, f.Name, f.MimeType, f.HasPreview, f.Path))
            .FirstOrDefaultAsync(ct);
    }
}