using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class EssentiaBatchRepository(AlexandriaDbContext context) : IEssentiaBatchRepository
{
    private readonly DbSet<EssentiaBatch> _batches = context.EssentiaBatches;

    public async Task<EssentiaBatch?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _batches.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<EssentiaBatch?> FirstOrDefaultAsync(
        Expression<Func<EssentiaBatch, bool>> predicate,
        CancellationToken ct = default)
        => await _batches.FirstOrDefaultAsync(predicate, ct);

    public async Task<IEnumerable<EssentiaBatch>> FindAsync(
        Expression<Func<EssentiaBatch, bool>> predicate,
        CancellationToken ct = default)
        => await _batches.Where(predicate).ToListAsync(ct);

    public async Task<EssentiaBatch> AddAsync(EssentiaBatch entity, CancellationToken ct = default)
    {
        var result = await _batches.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<IEnumerable<EssentiaBatch>> AddRangeAsync(
        IEnumerable<EssentiaBatch> entities,
        CancellationToken ct = default)
    {
        var list = entities.ToList();
        await _batches.AddRangeAsync(list, ct);
        await context.SaveChangesAsync(ct);
        return list;
    }

    public void Update(EssentiaBatch entity) => _batches.Update(entity);

    public void Remove(EssentiaBatch entity) => _batches.Remove(entity);

    public void RemoveRange(IEnumerable<EssentiaBatch> entities) => _batches.RemoveRange(entities);

    public async Task<int> CountAsync(
        Expression<Func<EssentiaBatch, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        var q = _batches.AsQueryable();
        if (predicate is not null)
            q = q.Where(predicate);
        return await q.CountAsync(ct);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<EssentiaBatch, bool>> predicate,
        CancellationToken ct = default)
        => await _batches.AnyAsync(predicate, ct);

    public async Task<EssentiaBatch?> GetWithFilesAsync(Guid batchId, CancellationToken ct = default)
        => await _batches
            .Include(b => b.Files.Where(f => f.DeletedAt == null))
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);

    public async Task<IEnumerable<EssentiaBatch>> GetDispatchedSinceAsync(DateTime cutoff,
        CancellationToken ct = default)
        => await _batches
            .AsNoTracking()
            .Where(b => b.Status == EssentiaBatchStatus.Dispatched && b.DispatchedAt < cutoff)
            .ToListAsync(ct);

    public async Task<int> MarkTimedOutAsync(IEnumerable<Guid> batchIds, CancellationToken ct = default)
    {
        var ids = batchIds.ToList();
        if (ids.Count == 0) return 0;

        return await _batches
            .Where(b => ids.Contains(b.Id) && b.Status == EssentiaBatchStatus.Dispatched)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, EssentiaBatchStatus.TimedOut)
                .SetProperty(b => b.CompletedAt, DateTime.UtcNow)
                .SetProperty(b => b.UpdatedAt, DateTime.UtcNow), ct);
    }
}