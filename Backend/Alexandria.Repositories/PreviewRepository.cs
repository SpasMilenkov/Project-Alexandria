using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Dto.Previews;
using Alexandria.Dto.PreviewsStats;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class PreviewRepository(AlexandriaDbContext context) : IPreviewRepository
{
    private readonly DbSet<Preview> _previews = context.Previews;

    public async Task<Preview?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _previews.FindAsync(new object[] { id }, ct);
    }

    public async Task<Preview?> FirstOrDefaultAsync(Expression<Func<Preview, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _previews.FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IEnumerable<Preview>> GetAllAsync(CancellationToken ct = default)
    {
        return await _previews.ToListAsync(ct);
    }

    public async Task<IEnumerable<Preview>> FindAsync(Expression<Func<Preview, bool>> predicate,
        CancellationToken ct = default)
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
        // DbSet.Remove walks the entity graph: a populated Version navigation
        // (e.g. from an AsNoTracking Include, which materializes a distinct
        // FileVersion instance per row) collides with an already-tracked
        // FileVersion of the same id. Only the Preview row is deleted here.
        entity.Version = null;
        _previews.Remove(entity);
    }

    public void RemoveRange(IEnumerable<Preview> entities)
    {
        var list = entities.ToList();
        foreach (var entity in list)
            entity.Version = null;
        _previews.RemoveRange(list);
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

    public async Task<IReadOnlyList<PreviewKindTotals>> GetKindTotalsAsync(
        CancellationToken ct = default)
    {
        return await _previews
            .AsNoTracking()
            .GroupBy(p => p.Kind)
            .Select(g => new PreviewKindTotals(g.Key, g.Count(), g.Sum(p => p.Size)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Preview>> GetCreatedBetweenAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await _previews
            .AsNoTracking()
            .Where(p => p.CreatedAt >= from && p.CreatedAt < to)
            .ToListAsync(ct);
    }

    public async Task<(long TotalSize, int Count, IReadOnlyList<PreviewKindTotals> ByKind)> GetStorageByUserAsync(
        Guid userId, CancellationToken ct = default)
    {
        var query = _previews
            .AsNoTracking()
            .Where(p => p.DeletedAt == null)
            .Join(context.FileVersions.AsNoTracking(),
                p => p.VersionId, v => v.Id, (p, v) => new { Preview = p, Version = v })
            .Join(context.Files.AsNoTracking(),
                x => x.Version.FileId, f => f.Id, (x, f) => new { x.Preview, x.Version, File = f })
            .Where(x => x.File.OwnerId == userId
                        && x.File.DeletedAt == null
                        && x.Version.DeletedAt == null);

        var totalSize = await query.SumAsync(x => (long?)x.Preview.Size, ct) ?? 0;
        var count = await query.CountAsync(ct);
        var byKind = await query
            .GroupBy(x => x.Preview.Kind)
            .Select(g => new PreviewKindTotals(g.Key, g.Count(), g.Sum(x => x.Preview.Size)))
            .ToListAsync(ct);

        return (totalSize, count, byKind);
    }

    public async Task<Dictionary<Guid, long>> GetSizeByOwnerAsync(CancellationToken ct = default)
    {
        return await _previews
            .AsNoTracking()
            .Where(p => p.DeletedAt == null)
            .Join(context.FileVersions.AsNoTracking(),
                p => p.VersionId, v => v.Id, (p, v) => new { Preview = p, Version = v })
            .Join(context.Files.AsNoTracking(),
                x => x.Version.FileId, f => f.Id, (x, f) => new { x.Preview, x.Version, File = f })
            .Where(x => x.File.DeletedAt == null && x.Version.DeletedAt == null)
            .GroupBy(x => x.File.OwnerId)
            .Select(g => new
            {
                OwnerId = g.Key,
                TotalSize = g.Sum(x => x.Preview.Size)
            })
            .ToDictionaryAsync(x => x.OwnerId, x => x.TotalSize, ct);
    }

    public async Task<Preview?> GetWithFileAsync(Guid previewId, CancellationToken ct = default)
    {
        return await _previews
            .AsNoTracking()
            .Include(p => p.Version)
            .ThenInclude(v => v!.File)
            .FirstOrDefaultAsync(p => p.Id == previewId && p.DeletedAt == null, ct);
    }

    public async Task<IReadOnlyList<Preview>> GetByFileAsync(
        Guid fileId, DateTime? createdBefore, CancellationToken ct = default)
    {
        var query = _previews
            .AsNoTracking()
            .Include(p => p.Version)
            .Where(p => p.DeletedAt == null && p.Version != null && p.Version.FileId == fileId);

        if (createdBefore.HasValue)
            query = query.Where(p => p.CreatedAt < createdBefore.Value);

        return await query.ToListAsync(ct);
    }

    public async Task<PaginatedResult<UserPreviewDto>> GetListByUserAsync(
        Guid userId, Guid? fileId, DateTime? createdBefore, int page, int pageSize,
        CancellationToken ct = default)
    {
        var query = _previews
            .AsNoTracking()
            .Where(p => p.DeletedAt == null)
            .Join(context.FileVersions.AsNoTracking(),
                p => p.VersionId, v => v.Id, (p, v) => new { Preview = p, Version = v })
            .Join(context.Files.AsNoTracking(),
                x => x.Version.FileId, f => f.Id, (x, f) => new { x.Preview, x.Version, File = f })
            .Where(x => x.File.OwnerId == userId
                        && x.File.DeletedAt == null
                        && x.Version.DeletedAt == null);

        if (fileId.HasValue)
            query = query.Where(x => x.File.Id == fileId.Value);

        if (createdBefore.HasValue)
            query = query.Where(x => x.Preview.CreatedAt < createdBefore.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.Preview.CreatedAt)
            .ThenBy(x => x.Preview.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new UserPreviewDto(
                x.Preview.Id,
                x.File.Id,
                x.File.Name,
                x.Preview.Kind,
                x.Preview.Size,
                x.Preview.CreatedAt))
            .ToListAsync(ct);

        return new PaginatedResult<UserPreviewDto>
        {
            Items = items,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<IReadOnlyList<Preview>> GetMissingSizesAsync(
        int take, CancellationToken ct = default)
    {
        return await _previews
            .Include(p => p.Version)
            .Where(p => p.Size <= 0 && p.DeletedAt == null)
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<bool> TryBackfillSizeAsync(
        Guid previewId, long size, CancellationToken ct = default)
    {
        var rows = await _previews
            .Where(p => p.Id == previewId && p.Size <= 0)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Size, size), ct);
        return rows > 0;
    }
}