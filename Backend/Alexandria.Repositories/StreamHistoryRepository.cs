using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class StreamHistoryRepository(AlexandriaDbContext context) : IStreamHistoryRepository
{
    private readonly DbSet<StreamHistory> _history = context.StreamHistory;

    public async Task<StreamHistory?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _history.FirstOrDefaultAsync(h => h.Id == id, ct);

    public async Task<StreamHistory?> FirstOrDefaultAsync(
        Expression<Func<StreamHistory, bool>> predicate,
        CancellationToken ct = default)
        => await _history.FirstOrDefaultAsync(predicate, ct);

    public async Task<IEnumerable<StreamHistory>> FindAsync(
        Expression<Func<StreamHistory, bool>> predicate,
        CancellationToken ct = default)
        => await _history.Where(predicate).ToListAsync(ct);

    public async Task<StreamHistory> AddAsync(StreamHistory entity, CancellationToken ct = default)
    {
        var result = await _history.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<IEnumerable<StreamHistory>> AddRangeAsync(
        IEnumerable<StreamHistory> entities,
        CancellationToken ct = default)
    {
        var list = entities.ToList();
        await _history.AddRangeAsync(list, ct);
        await context.SaveChangesAsync(ct);
        return list;
    }

    public void Update(StreamHistory entity) => _history.Update(entity);

    public void Remove(StreamHistory entity) => _history.Remove(entity);

    public void RemoveRange(IEnumerable<StreamHistory> entities) => _history.RemoveRange(entities);

    public async Task<int> CountAsync(
        Expression<Func<StreamHistory, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        var q = _history.AsQueryable();
        if (predicate is not null)
            q = q.Where(predicate);
        return await q.CountAsync(ct);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<StreamHistory, bool>> predicate,
        CancellationToken ct = default)
        => await _history.AnyAsync(predicate, ct);

    public async Task<StreamHistory?> GetByUserAndFileAsync(
        Guid userId,
        Guid fileId,
        CancellationToken ct = default)
        => await _history
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.UserId == userId && h.FileId == fileId, ct);

    public async Task<PaginatedResult<StreamHistory>> FindHistoryAsync(
        StreamHistoryQuery query,
        CancellationToken ct = default)
    {
        var q = _history.AsQueryable();

        if (query.UserId.HasValue)
            q = q.Where(h => h.UserId == query.UserId.Value);

        if (query.FileId.HasValue)
            q = q.Where(h => h.FileId == query.FileId.Value);

        if (query.Completed.HasValue)
            q = q.Where(h => h.Completed == query.Completed.Value);

        if (query.AccessedAfter.HasValue)
            q = q.Where(h => h.LastAccessedAt >= query.AccessedAfter.Value.ToUniversalTime());

        if (query.AccessedBefore.HasValue)
            q = q.Where(h => h.LastAccessedAt <= query.AccessedBefore.Value.ToUniversalTime());

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .AsNoTracking()
            .OrderByDescending(h => h.LastAccessedAt)
            .Skip((query.CurrentPage - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PaginatedResult<StreamHistory>
        {
            Items = items,
            TotalCount = totalCount,
            CurrentPage = query.CurrentPage,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
        };
    }

    public async Task UpsertPositionAsync(
        Guid userId,
        Guid fileId,
        long positionSeconds,
        bool completed,
        CancellationToken ct = default)
    {
        var exists = await _history.AnyAsync(h => h.UserId == userId && h.FileId == fileId, ct);

        if (!exists)
        {
            await _history.AddAsync(new StreamHistory
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FileId = fileId,
                PositionSeconds = positionSeconds,
                Completed = completed,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            }, ct);
            await context.SaveChangesAsync(ct);
        }
        else
        {
            await _history
                .Where(h => h.UserId == userId && h.FileId == fileId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(h => h.PositionSeconds, positionSeconds)
                    .SetProperty(h => h.Completed, completed)
                    .SetProperty(h => h.LastAccessedAt, DateTime.UtcNow), ct);
        }
    }

    public async Task<IEnumerable<StreamHistory>> GetRecentByUserAsync(
        Guid userId,
        int count,
        CancellationToken ct = default)
        => await _history
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.LastAccessedAt)
            .Take(count)
            .ToListAsync(ct);
}