using System.Linq.Expressions;
using Common.Repositories;
using Data.Context;
using Microsoft.EntityFrameworkCore;
using Models;

namespace Repositories;

public class ContentObjectRepository : IContentObjectRepository
{
    private readonly DbSet<ContentObject> _dbSet;

    public ContentObjectRepository(AlexandriaDbContext context)
    {
        var context1 = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = context1.Set<ContentObject>();
    }

    public async Task<ContentObject?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, ct);
    }

    public async Task<ContentObject?> FirstOrDefaultAsync(
        Expression<Func<ContentObject, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<ContentObject?> HashExists(byte[] hash)
    {
        return await _dbSet.FirstOrDefaultAsync(c => c.Hash == hash);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _dbSet.Where(c => c.Id == id).ExecuteDeleteAsync();
    }

    public async Task<IEnumerable<ContentObject>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbSet.ToListAsync(ct);
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
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        entity.CreatedAt = DateTime.UtcNow;
        entity.RefCount = 1; // Initialize ref count for new content objects

        var entry = await _dbSet.AddAsync(entity, ct);
        return entry.Entity;
    }

    public async Task<IEnumerable<ContentObject>> AddRangeAsync(
        IEnumerable<ContentObject> entities,
        CancellationToken ct = default)
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));

        var contentObjects = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var entity in contentObjects)
        {
            entity.CreatedAt = now;
            if (entity.RefCount == 0)
                entity.RefCount = 1;
        }

        await _dbSet.AddRangeAsync(contentObjects, ct);
        return contentObjects;
    }

    public void Update(ContentObject entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        entity.UpdatedAt = DateTime.UtcNow;
        entity.RefCount = entity.RefCount;
        _dbSet.Update(entity);
    }

    public void Remove(ContentObject entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        entity.DeletedAt = DateTime.UtcNow;
        _dbSet.Update(entity);
    }

    public void RemoveRange(IEnumerable<ContentObject> entities)
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));

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
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        return await _dbSet.AnyAsync(predicate, ct);
    }

    // Additional methods specific to ContentObject management

    public async Task IncrementRefCountAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetByIdAsync(id, ct);
        if (entity != null)
        {
            entity.RefCount++;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.OrphanedAt = null; // Clear orphaned status when ref count increases
            Update(entity);
        }
    }

    public async Task DecrementRefCountAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetByIdAsync(id, ct);
        if (entity != null && entity.RefCount > 0)
        {
            entity.RefCount--;
            entity.UpdatedAt = DateTime.UtcNow;

            // Mark as orphaned if ref count reaches zero
            if (entity.RefCount == 0)
            {
                entity.OrphanedAt = DateTime.UtcNow;
            }

            Update(entity);
        }
    }

    public async Task<IEnumerable<ContentObject>> GetOrphanedContentObjectsAsync(
        DateTime olderThan,
        CancellationToken ct = default)
    {
        return await _dbSet
            .Where(co => co.RefCount == 0 &&
                         co.OrphanedAt.HasValue &&
                         co.OrphanedAt.Value < olderThan)
            .ToListAsync(ct);
    }

    public async Task<ContentObject?> GetByHashAsync(byte[] hash, CancellationToken ct = default)
    {
        if (hash == null || hash.Length == 0)
            throw new ArgumentException("Hash cannot be null or empty", nameof(hash));

        return await _dbSet
            .FirstOrDefaultAsync(co => co.Hash.SequenceEqual(hash), ct);
    }
}