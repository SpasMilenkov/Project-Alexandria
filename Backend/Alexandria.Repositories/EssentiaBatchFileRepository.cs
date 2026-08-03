using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class EssentiaBatchFileRepository(AlexandriaDbContext context) : IEssentiaBatchFileRepository
{
    private readonly DbSet<EssentiaBatchFile> _batchFiles = context.EssentiaBatchFiles;

    public async Task<EssentiaBatchFile?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _batchFiles.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<EssentiaBatchFile?> FirstOrDefaultAsync(
        Expression<Func<EssentiaBatchFile, bool>> predicate,
        CancellationToken ct = default)
        => await _batchFiles.FirstOrDefaultAsync(predicate, ct);

    public async Task<IEnumerable<EssentiaBatchFile>> FindAsync(
        Expression<Func<EssentiaBatchFile, bool>> predicate,
        CancellationToken ct = default)
        => await _batchFiles.Where(predicate).ToListAsync(ct);

    public async Task<EssentiaBatchFile> AddAsync(EssentiaBatchFile entity, CancellationToken ct = default)
    {
        var result = await _batchFiles.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<IEnumerable<EssentiaBatchFile>> AddRangeAsync(
        IEnumerable<EssentiaBatchFile> entities,
        CancellationToken ct = default)
    {
        var list = entities.ToList();
        await _batchFiles.AddRangeAsync(list, ct);
        await context.SaveChangesAsync(ct);
        return list;
    }

    public void Update(EssentiaBatchFile entity) => _batchFiles.Update(entity);

    public void Remove(EssentiaBatchFile entity) => _batchFiles.Remove(entity);

    public void RemoveRange(IEnumerable<EssentiaBatchFile> entities) => _batchFiles.RemoveRange(entities);

    public async Task<int> CountAsync(
        Expression<Func<EssentiaBatchFile, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        var q = _batchFiles.AsQueryable();
        if (predicate is not null)
            q = q.Where(predicate);
        return await q.CountAsync(ct);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<EssentiaBatchFile, bool>> predicate,
        CancellationToken ct = default)
        => await _batchFiles.AnyAsync(predicate, ct);

    public async Task<IEnumerable<EssentiaBatchFile>> GetByBatchAsync(Guid batchId, CancellationToken ct = default)
        => await _batchFiles
            .Where(f => f.BatchId == batchId && f.DeletedAt == null)
            .ToListAsync(ct);

    public async Task<int> UpdateStatusForBatchAsync(
        Guid batchId,
        EssentiaBatchFileStatus currentStatus,
        EssentiaBatchFileStatus newStatus,
        Guid? updatedBy,
        CancellationToken ct = default)
        => await _batchFiles
            .Where(f => f.BatchId == batchId && f.Status == currentStatus)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.Status, newStatus)
                .SetProperty(f => f.UpdatedAt, DateTime.UtcNow)
                .SetProperty(f => f.UpdatedBy, updatedBy), ct);
}