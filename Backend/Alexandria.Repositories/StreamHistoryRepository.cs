using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Dto.Files.Streaming;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class StreamHistoryRepository(AlexandriaDbContext context) : IStreamHistoryRepository
{
    private readonly DbSet<StreamHistory> _history = context.StreamHistories;
    private readonly DbSet<StreamSession> _sessions = context.StreamSessions;

    public async Task<StreamHistory?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _history.FindAsync(new object[] { id }, ct);

    public async Task<StreamHistory?> GetByIdAndUserIdAsync(Guid id, Guid userId, CancellationToken ct = default)
        => await _history
            .FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId && h.DeletedAt == null, ct);

    public async Task<StreamHistory?> GetByUserAndFileAsync(Guid userId, Guid fileId, CancellationToken ct = default)
        => await _history
            .FirstOrDefaultAsync(h => h.UserId == userId && h.FileId == fileId && h.DeletedAt == null, ct);

    public async Task<(ICollection<StreamHistoryDto> Items, int TotalCount)> FindAsync(
        Guid userId,
        StreamHistoryQuery query,
        CancellationToken ct = default)
    {
        var q = _history.Where(h => h.UserId == userId && h.DeletedAt == null);

        if (query.FileId.HasValue)
            q = q.Where(h => h.FileId == query.FileId.Value);

        if (query.Completed.HasValue)
            q = query.Completed.Value
                ? q.Where(h => h.TimesCompleted > 0)
                : q.Where(h => h.TimesCompleted == 0);

        if (query.LastAccessedAfter.HasValue)
            q = q.Where(h => h.LastAccessedAt >= query.LastAccessedAfter.Value);

        if (query.LastAccessedBefore.HasValue)
            q = q.Where(h => h.LastAccessedAt <= query.LastAccessedBefore.Value);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(h => h.LastAccessedAt)
            .Skip((query.CurrentPage - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(h => StreamHistoryDto.FromEntity(h))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<StreamHistory> CreateAsync(StreamHistory entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        var entry = await _history.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return entry.Entity;
    }

    public async Task<StreamHistory> UpdateAsync(StreamHistory entity, CancellationToken ct = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _history.Update(entity);
        await context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<StreamSession> CreateSessionAsync(StreamSession session, CancellationToken ct = default)
    {
        session.CreatedAt = DateTime.UtcNow;
        var entry = await _sessions.AddAsync(session, ct);
        await context.SaveChangesAsync(ct);
        return entry.Entity;
    }

    public async Task<StreamSession> UpdateSessionAsync(StreamSession session, CancellationToken ct = default)
    {
        session.UpdatedAt = DateTime.UtcNow;
        _sessions.Update(session);
        await context.SaveChangesAsync(ct);
        return session;
    }

    public async Task<StreamSession?> GetSessionByIdAsync(Guid sessionId, CancellationToken ct = default)
        => await _sessions.FindAsync(new object[] { sessionId }, ct);

    public async Task<IEnumerable<StreamSessionDto>> GetSessionsAsync(Guid streamHistoryId,
        CancellationToken ct = default)
        => await _sessions
            .Where(s => s.StreamHistoryId == streamHistoryId && s.DeletedAt == null)
            .OrderBy(s => s.StartedAt)
            .Select(s => StreamSessionDto.FromEntity(s))
            .ToListAsync(ct);

    // IRepository passthrough members
    public async Task<IEnumerable<StreamHistory>> GetAllAsync(CancellationToken ct = default)
        => await _history.ToListAsync(ct);

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
        entity.CreatedAt = DateTime.UtcNow;
        var entry = await _history.AddAsync(entity, ct);
        return entry.Entity;
    }

    public Task<IEnumerable<StreamHistory>> AddRangeAsync(IEnumerable<StreamHistory> entities,
        CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public void Update(StreamHistory entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _history.Update(entity);
    }

    public void Remove(StreamHistory entity) => _history.Remove(entity);

    public void RemoveRange(IEnumerable<StreamHistory> entities) => _history.RemoveRange(entities);

    public async Task<int> CountAsync(
        Expression<Func<StreamHistory, bool>>? predicate = null,
        CancellationToken ct = default)
        => predicate == null
            ? await _history.CountAsync(ct)
            : await _history.CountAsync(predicate, ct);

    public async Task<bool> ExistsAsync(
        Expression<Func<StreamHistory, bool>> predicate,
        CancellationToken ct = default)
        => await _history.AnyAsync(predicate, ct);
}