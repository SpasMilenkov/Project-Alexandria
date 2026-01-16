using System.Linq.Expressions;
using Common.Repositories;
using Data.Context;
using Microsoft.EntityFrameworkCore;
using Models;

namespace Repositories;

public class FileVersionRepository : IFileVersionRepository
{
    private readonly DbSet<FileVersion> _dbSet;
    private readonly AlexandriaDbContext _context;

    public FileVersionRepository(AlexandriaDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = _context.Set<FileVersion>();
    }

    public async Task<FileVersion?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(fv => fv.ContentObject)
            .Include(fv => fv.File)
            .FirstOrDefaultAsync(fv => fv.Id == id, ct);
    }


    public async Task<FileVersion?> FirstOrDefaultAsync(
        Expression<Func<FileVersion, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet
            .Include(fv => fv.ContentObject)
            .Include(fv => fv.File)
            .FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IEnumerable<FileVersion>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbSet
            .Include(fv => fv.ContentObject)
            .Include(fv => fv.File)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<FileVersion>> FindAsync(
        Expression<Func<FileVersion, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet
            .Include(fv => fv.ContentObject)
            .Include(fv => fv.File)
            .Where(predicate)
            .ToListAsync(ct);
    }

    public async Task<FileVersion> AddAsync(FileVersion entity, CancellationToken ct = default)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        entity.CreatedAt = DateTime.UtcNow;

        var entry = await _dbSet.AddAsync(entity, ct);
        return entry.Entity;
    }

    public async Task<IEnumerable<FileVersion>> AddRangeAsync(
        IEnumerable<FileVersion> entities,
        CancellationToken ct = default)
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));

        var fileVersions = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var entity in fileVersions)
        {
            entity.CreatedAt = now;
        }

        await _dbSet.AddRangeAsync(fileVersions, ct);
        return fileVersions;
    }

    public void Update(FileVersion entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        entity.UpdatedAt = DateTime.UtcNow;
        _dbSet.Update(entity);
    }

    public void Remove(FileVersion entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        entity.DeletedAt = DateTime.UtcNow;
        _dbSet.Update(entity);
    }

    public void RemoveRange(IEnumerable<FileVersion> entities)
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));

        var now = DateTime.UtcNow;
        var fileVersions = entities.ToList();

        foreach (var entity in fileVersions)
        {
            entity.DeletedAt = now;
        }

        _dbSet.UpdateRange(fileVersions);
    }

    public async Task<int> CountAsync(
        Expression<Func<FileVersion, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        return predicate == null
            ? await _dbSet.CountAsync(ct)
            : await _dbSet.CountAsync(predicate, ct);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<FileVersion, bool>> predicate,
        CancellationToken ct = default)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        return await _dbSet.AnyAsync(predicate, ct);
    }

    public async Task<int> DeleteAllVersionsOfAFile(Guid fileId, CancellationToken ct = default)
    {
        // Load all versions that need to be deleted
        var versions = await _context.FileVersions
            .Where(v => v.FileId == fileId)
            .ToListAsync(ct);

        // Get content object IDs and their reference counts
        var contentRefCounts = versions
            .GroupBy(v => v.ContentObjectId)
            .Select(g => new { ContentObjectId = g.Key, Count = g.Count() })
            .ToList();

        // Load all affected content objects
        var contentObjectIds = contentRefCounts.Select(c => c.ContentObjectId).ToList();
        var contentObjects = await _context.ContentObjects
            .Where(c => contentObjectIds.Contains(c.Id))
            .ToListAsync(ct);

        // Decrement ref counts
        foreach (var item in contentRefCounts)
        {
            var contentObject = contentObjects.FirstOrDefault(c => c.Id == item.ContentObjectId);
            if (contentObject == null) continue;
            contentObject.RefCount -= item.Count;

            if (contentObject.RefCount == 0)
            {
                contentObject.OrphanedAt = DateTime.UtcNow;
            }
        }

        // Soft delete all versions
        foreach (var version in versions)
        {
            version.DeletedAt = DateTime.UtcNow;
        }

        return versions.Count;
    }

    public async Task<int> DeleteFileVersions(Guid[] fileIds, Guid ownerId, CancellationToken ct = default)
    {
        var refDeltas = await _context.FileVersions
            .Where(v => fileIds.Contains(v.FileId) && v.File.OwnerId == ownerId)
            .GroupBy(v => v.ContentObjectId)
            .Select(g => new
            {
                ContentObjectId = g.Key,
                Delta = g.Count()
            })
            .ToListAsync(ct);

        var deletedCount = await _context.FileVersions
            .Where(v => fileIds.Contains(v.FileId) && v.File.OwnerId == ownerId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(v => v.DeletedAt, _ => DateTime.UtcNow),
                ct);


        var now = DateTime.UtcNow;

        foreach (var item in refDeltas)
        {
            await _context.ContentObjects
                .Where(c => c.Id == item.ContentObjectId)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(c => c.RefCount, c => c.RefCount - item.Delta)
                        .SetProperty(
                            c => c.OrphanedAt,
                            c => c.RefCount - item.Delta == 0 && c.OrphanedAt == null
                                ? now
                                : c.OrphanedAt),
                    ct);
        }

        return deletedCount;
    }
}