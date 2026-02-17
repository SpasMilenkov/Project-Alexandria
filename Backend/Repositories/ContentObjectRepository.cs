using System.Linq.Expressions;
using Common.Repositories;
using Data.Context;
using Microsoft.EntityFrameworkCore;
using Models;

namespace Repositories;

public class ContentObjectRepository(AlexandriaDbContext context) : IContentObjectRepository
{
    private readonly DbSet<ContentObject> _dbSet = context.Set<ContentObject>();

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

    public async Task<ContentObject?> HashExists(byte[] hash, CancellationToken ct = default)
    {
        return await _dbSet
             .FirstOrDefaultAsync(co =>
                 co.Hash == hash &&
                 co.OrphanedAt == null &&
                 co.DeletedAt == null,
                 ct);
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
        }

        await _dbSet.AddRangeAsync(contentObjects, ct);
        return contentObjects;
    }

    public void Update(ContentObject entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        entity.UpdatedAt = DateTime.UtcNow;
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

    public async Task<int> MarkOrphaned(DateTime dateTime, CancellationToken ct = default)
    {
        return await _dbSet
                .Where(co =>
                    co.OrphanedAt == null &&
                    co.DeletedAt == null &&
                    !context.FileVersions.Any(fv =>
                        fv.ContentObjectId == co.Id &&
                        fv.DeletedAt == null))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(co => co.OrphanedAt, _ => dateTime),
                    ct);
    }

    public async Task<int> ClearOrphaned(DateTime dateTime, CancellationToken ct = default)
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
}
