using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class FileEnrichmentRepository(AlexandriaDbContext context) : IFileEnrichmentRepository
{
    private readonly DbSet<FileEnrichment> _enrichments = context.FileEnrichments;

    public async Task<FileEnrichment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _enrichments.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<FileEnrichment?> FirstOrDefaultAsync(
        Expression<Func<FileEnrichment, bool>> predicate,
        CancellationToken ct = default)
        => await _enrichments.FirstOrDefaultAsync(predicate, ct);

    public async Task<IEnumerable<FileEnrichment>> FindAsync(
        Expression<Func<FileEnrichment, bool>> predicate,
        CancellationToken ct = default)
        => await _enrichments.Where(predicate).ToListAsync(ct);

    public async Task<FileEnrichment> AddAsync(FileEnrichment entity, CancellationToken ct = default)
    {
        var result = await _enrichments.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<IEnumerable<FileEnrichment>> AddRangeAsync(
        IEnumerable<FileEnrichment> entities,
        CancellationToken ct = default)
    {
        var list = entities.ToList();
        await _enrichments.AddRangeAsync(list, ct);
        await context.SaveChangesAsync(ct);
        return list;
    }

    public void Update(FileEnrichment entity) => _enrichments.Update(entity);

    public void Remove(FileEnrichment entity) => _enrichments.Remove(entity);

    public void RemoveRange(IEnumerable<FileEnrichment> entities) => _enrichments.RemoveRange(entities);

    public async Task<int> CountAsync(
        Expression<Func<FileEnrichment, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        var q = _enrichments.AsQueryable();
        if (predicate is not null)
            q = q.Where(predicate);
        return await q.CountAsync(ct);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<FileEnrichment, bool>> predicate,
        CancellationToken ct = default)
        => await _enrichments.AnyAsync(predicate, ct);

    public async Task<FileEnrichment> UpsertAsync(FileEnrichment row, CancellationToken ct = default)
    {
        var existing = await _enrichments.FirstOrDefaultAsync(
            e => e.FileId == row.FileId
                 && e.Analyzer == row.Analyzer
                 && e.Version == row.Version, ct);

        if (existing is not null)
        {
            existing.PayloadJson = row.PayloadJson;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = row.UpdatedBy;
            await context.SaveChangesAsync(ct);
            return existing;
        }

        var result = await _enrichments.AddAsync(row, ct);
        await context.SaveChangesAsync(ct);
        return result.Entity;
    }
}