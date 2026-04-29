using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class UploadRepository(AlexandriaDbContext context) : IUploadRepository
{
    private readonly DbSet<Upload> _uploads = context.Uploads;

    public async Task<Upload?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _uploads
            .Where(u => u.DeletedAt == null)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<Upload?> FirstOrDefaultAsync(Expression<Func<Upload, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _uploads
            .Where(u => u.DeletedAt == null)
            .FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IEnumerable<Upload>> GetAllAsync(CancellationToken ct = default)
    {
        return await _uploads
            .Where(u => u.DeletedAt == null)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Upload>> FindAsync(Expression<Func<Upload, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _uploads
            .Where(u => u.DeletedAt == null)
            .Where(predicate)
            .ToListAsync(ct);
    }

    public async Task<Upload> AddAsync(Upload entity, CancellationToken ct = default)
    {
        // Set audit fields
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = null;
        entity.DeletedAt = null;

        var result = await _uploads.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<IEnumerable<Upload>> AddRangeAsync(IEnumerable<Upload> entities, CancellationToken ct = default)
    {
        var uploads = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var upload in uploads)
        {
            upload.CreatedAt = now;
            upload.UpdatedAt = null;
            upload.DeletedAt = null;
        }

        await _uploads.AddRangeAsync(uploads, ct);
        await context.SaveChangesAsync(ct);
        return uploads;
    }

    public void Update(Upload entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _uploads.Update(entity);
    }

    public void Remove(Upload entity)
    {
        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _uploads.Update(entity);
    }

    public void RemoveRange(IEnumerable<Upload> entities)
    {
        var now = DateTime.UtcNow;
        var enumerable = entities as Upload[] ?? entities.ToArray();
        foreach (var entity in enumerable)
        {
            entity.DeletedAt = now;
            entity.UpdatedAt = now;
        }

        _uploads.UpdateRange(enumerable);
    }

    public async Task<int> CountAsync(Expression<Func<Upload, bool>>? predicate = null, CancellationToken ct = default)
    {
        var query = _uploads.Where(u => u.DeletedAt == null);

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        return await query.CountAsync(ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<Upload, bool>> predicate, CancellationToken ct = default)
    {
        return await _uploads
            .Where(u => u.DeletedAt == null)
            .AnyAsync(predicate, ct);
    }
}