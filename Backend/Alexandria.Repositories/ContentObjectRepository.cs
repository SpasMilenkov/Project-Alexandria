using System.Linq.Expressions;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class ContentObjectRepository(AlexandriaDbContext context) : IContentObjectRepository
{
    private readonly DbSet<ContentObject> _dbSet = context.Set<ContentObject>();

    public async Task<ContentObject?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet.FindAsync([id], ct);
    }

    public async Task<ContentObject?> FirstOrDefaultAsync(
        Expression<Func<ContentObject, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate, ct);
    }

    /// <summary>
    /// Deduplication lookup. Matches live and orphaned rows so finalize can
    /// revive an orphaned row instead of aliasing its storage key with a second
    /// row. Soft-deleted rows are excluded. Uniqueness is enforced by
    /// ix_contentobjects_not_deleted, so at most one row matches.
    /// </summary>
    public async Task<ContentObject?> HashExistsAsync(byte[] hash, CancellationToken ct = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(co =>
                    co.Hash == hash &&
                    co.DeletedAt == null,
                ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _dbSet.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task<IEnumerable<ContentObject>> FindAsync(
        Expression<Func<ContentObject, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet
            .Include(c => c.Upload)
            .Where(predicate)
            .ToListAsync(ct);
    }

    public async Task<ContentObject> AddAsync(ContentObject entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.CreatedAt = DateTime.UtcNow;

        var entry = await _dbSet.AddAsync(entity, ct);
        return entry.Entity;
    }

    public async Task<IEnumerable<ContentObject>> AddRangeAsync(
        IEnumerable<ContentObject> entities,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var contentObjects = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var entity in contentObjects)
        {
            entity.CreatedAt = now;
        }

        await _dbSet.AddRangeAsync(contentObjects, ct);
        return contentObjects;
    }

    public void Update(ContentObject entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt = DateTime.UtcNow;
        _dbSet.Update(entity);
    }

    public void Remove(ContentObject entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.DeletedAt = DateTime.UtcNow;
        _dbSet.Update(entity);
    }

    public void RemoveRange(IEnumerable<ContentObject> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var now = DateTime.UtcNow;
        var contentObjects = entities.ToList();

        foreach (var entity in contentObjects)
        {
            entity.DeletedAt = now;
        }

        _dbSet.UpdateRange(contentObjects);
    }

    public async Task<int> CountAsync(
        Expression<Func<ContentObject, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        return predicate == null
            ? await _dbSet.CountAsync(ct)
            : await _dbSet.CountAsync(predicate, ct);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<ContentObject, bool>> predicate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return await _dbSet.AnyAsync(predicate, ct);
    }

    public async Task<int> MarkOrphaned(DateTime time, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(co =>
                co.OrphanedAt == null &&
                co.DeletedAt == null &&
                !context.FileVersions.Any(fv =>
                    fv.ContentObjectId == co.Id &&
                    fv.DeletedAt == null))
            .ExecuteUpdateAsync(
                s => s.SetProperty(co => co.OrphanedAt, _ => time),
                ct);
    }

    public async Task<int> ClearOrphaned(DateTime time, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(co =>
                co.OrphanedAt != null &&
                co.DeletedAt == null &&
                context.FileVersions.Any(fv =>
                    fv.ContentObjectId == co.Id &&
                    fv.DeletedAt == null))
            .ExecuteUpdateAsync(
                s => s.SetProperty(co => co.OrphanedAt, _ => (DateTime?)null),
                ct);
    }

    /// <inheritdoc/>
    public async Task<bool> TryClaimOrphanedDeleteAsync(Guid id, DateTime cutoff, CancellationToken ct = default)
    {
        var deleted = await _dbSet
            .Where(co =>
                co.Id == id &&
                co.DeletedAt == null &&
                co.OrphanedAt != null &&
                co.OrphanedAt < cutoff &&
                !context.FileVersions.Any(fv =>
                    fv.ContentObjectId == co.Id &&
                    fv.DeletedAt == null))
            .ExecuteDeleteAsync(ct);

        return deleted == 1;
    }

    /// <inheritdoc/>
    public async Task<List<ContentObject>> GetByHashesAsync(IEnumerable<byte[]> hashes, CancellationToken ct = default)
    {
        var hashList = hashes.ToList();

        if (hashList.Count == 0)
            return [];

        return await _dbSet
            .AsNoTracking()
            .Where(co =>
                co.DeletedAt == null &&
                hashList.Contains(co.Hash))
            .ToListAsync(ct);
    }

    public async Task<bool> IsVideo(Guid versionId,
        Guid userId,
        CancellationToken ct = default)
    {
        var result = await context.FileVersions
                         .Where(v => v.Id == versionId
                                     && v.File.OwnerId == userId
                                     && v.ContentObject.OrphanedAt == null
                                     && v.ContentObject.DeletedAt == null)
                         .Select(v => v.MimeType)
                         .FirstOrDefaultAsync(ct)
                     ?? throw new ContentObjectNotFoundException(versionId);

        return result.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
    }
}