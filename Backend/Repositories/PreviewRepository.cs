using System.Linq.Expressions;
using Common;
using Common.Repositories;
using Data.Context;
using DTO;
using Microsoft.EntityFrameworkCore;
using Models;

namespace Repositories;

public class PreviewRepository(AlexandriaDbContext context) : IPreviewRepository
{
    private readonly DbSet<Preview> _previews = context.Previews;

    public async Task<Preview?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _previews.FindAsync(new object[] { id }, ct);
    }

    public async Task<Preview?> FirstOrDefaultAsync(Expression<Func<Preview, bool>> predicate, CancellationToken ct = default)
    {
        return await _previews.FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IEnumerable<Preview>> GetAllAsync(CancellationToken ct = default)
    {
        return await _previews.ToListAsync(ct);
    }

    public async Task<IEnumerable<Preview>> FindAsync(Expression<Func<Preview, bool>> predicate, CancellationToken ct = default)
    {
        return await _previews.Where(predicate).ToListAsync(ct);
    }

    public async Task<Preview> AddAsync(Preview entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        var entry = await _previews.AddAsync(entity, ct);
        return entry.Entity;
    }

    public async Task<IEnumerable<Preview>> AddRangeAsync(IEnumerable<Preview> entities, CancellationToken ct = default)
    {
        var previewList = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var entity in previewList)
        {
            entity.CreatedAt = now;
        }

        await _previews.AddRangeAsync(previewList, ct);
        return previewList;
    }

    public void Update(Preview entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _previews.Update(entity);
    }

    public void Remove(Preview entity)
    {
        _previews.Remove(entity);
    }

    public void RemoveRange(IEnumerable<Preview> entities)
    {
        _previews.RemoveRange(entities);
    }

    public async Task<int> CountAsync(Expression<Func<Preview, bool>>? predicate = null, CancellationToken ct = default)
    {
        return predicate == null
            ? await _previews.CountAsync(ct)
            : await _previews.CountAsync(predicate, ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<Preview, bool>> predicate, CancellationToken ct = default)
    {
        return await _previews.AnyAsync(predicate, ct);
    }

    public async Task<Preview> CreateAsync(Preview file, CancellationToken ct = default)
    {
        file.CreatedAt = DateTime.UtcNow;
        var entry = await _previews.AddAsync(file, ct);
        await context.SaveChangesAsync(ct);
        return entry.Entity;
    }

    public async Task<Preview> UpdateAsync(Preview file, CancellationToken ct = default)
    {
        file.UpdatedAt = DateTime.UtcNow;
        _previews.Update(file);
        await context.SaveChangesAsync(ct);
        return file;
    }
}