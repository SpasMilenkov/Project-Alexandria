using System.Linq.Expressions;
using Alexandria.Common.Exceptions.Streaming;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Dto.Files.Streaming.Shuffle;
using Alexandria.Dto.Files.Streaming.Stats;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class StreamHistoryRepository(AlexandriaDbContext context) : IStreamHistoryRepository
{
    private readonly DbSet<StreamHistory> _history = context.StreamHistories;
    private readonly DbSet<StreamSession> _sessions = context.StreamSessions;

    public async Task<StreamHistory?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _history.FindAsync([id], ct);

    public async Task<StreamHistory?> GetByIdAndUserIdAsync(Guid id, Guid userId, CancellationToken ct = default)
        => await _history
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId && h.DeletedAt == null, ct);

    public async Task<StreamHistory?> GetByUserAndFileAsync(Guid userId, Guid fileId, CancellationToken ct = default)
        => await _history
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.UserId == userId && h.FileId == fileId && h.DeletedAt == null, ct);

    public async Task<PaginatedResult<StreamHistoryDto>> FindAsync(
        Guid userId,
        StreamHistoryQuery query,
        CancellationToken ct = default)
    {
        var q = _history.Where(h => h.UserId == userId && h.DeletedAt == null);

        if (query.FileId.HasValue)
            q = q.Where(h => h.FileId == query.FileId.Value);

        if (query.Qualified.HasValue)
            q = query.Qualified.Value
                ? q.Where(h => h.QualifiedPlayCount > 0)
                : q.Where(h => h.QualifiedPlayCount == 0);

        if (query.LastAccessedAfter.HasValue)
            q = q.Where(h => h.LastAccessedAt >= query.LastAccessedAfter.Value);

        if (query.LastAccessedBefore.HasValue)
            q = q.Where(h => h.LastAccessedAt <= query.LastAccessedBefore.Value);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(h => h.LastAccessedAt)
            .Skip((query.CurrentPage - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(h => new StreamHistoryDto
            {
                Id = h.Id,
                FileId = h.FileId,
                Title = h.File.MediaMetadata!.Title ?? h.File.Name,
                PositionSeconds = h.PositionSeconds,
                MaxPositionReachedSeconds = h.MaxPositionReachedSeconds,
                TotalListenedSeconds = h.TotalListenedSeconds,
                QualifiedPlayCount = h.QualifiedPlayCount,
                LastPlayedAt = h.LastPlayedAt,
                HasFinished = h.HasFinished,
                LastAccessedAt = h.LastAccessedAt,
                CreatedAt = h.CreatedAt,
                UpdatedAt = h.UpdatedAt
            })
            .ToListAsync(ct);

        return new PaginatedResult<StreamHistoryDto>
        {
            Items = items,
            CurrentPage = query.CurrentPage,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize),
            TotalCount = totalCount
        };
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

    public async Task UpdatePositionAsync(Guid historyId, Guid userId, long positionSeconds,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var updated = await _history
            .Where(h => h.Id == historyId && h.UserId == userId && h.DeletedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(h => h.PositionSeconds, h => now >= h.LastAccessedAt ? positionSeconds : h.PositionSeconds)
                .SetProperty(h => h.LastAccessedAt, h => now >= h.LastAccessedAt ? now : h.LastAccessedAt)
                .SetProperty(h => h.UpdatedAt, now), ct);

        if (updated == 0)
            throw new StreamHistoryNotFoundException(historyId);
    }

    public async Task<StreamHistory> CloseSessionAsync(StreamSession session, Guid userId,
        CancellationToken ct = default)
    {
        var closedAt = session.EndedAt ?? throw new ArgumentException("A close requires an end time.", nameof(session));
        var playIncrement = session.IsQualifiedPlay ? 1 : 0;

        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            var ownedSession = _sessions.Where(s => s.Id == session.Id
                                                   && s.StreamHistoryId == session.StreamHistoryId
                                                   && s.DeletedAt == null
                                                   && s.StreamHistory.UserId == userId
                                                   && s.StreamHistory.DeletedAt == null);

            var closed = await ownedSession
                .Where(s => s.EndedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.EndPositionSeconds, session.EndPositionSeconds)
                    .SetProperty(s => s.ListenedSeconds, session.ListenedSeconds)
                    .SetProperty(s => s.IsQualifiedPlay, session.IsQualifiedPlay)
                    .SetProperty(s => s.PlaybackFinished, session.PlaybackFinished)
                    .SetProperty(s => s.EndedAt, closedAt)
                    .SetProperty(s => s.UpdatedAt, closedAt), ct);

            if (closed == 0 && !await ownedSession.AnyAsync(s => s.EndedAt != null, ct))
                throw new StreamSessionNotFoundException(session.Id);

            if (closed != 0)
            {
                var updated = await _history
                    .Where(h => h.Id == session.StreamHistoryId && h.UserId == userId && h.DeletedAt == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(h => h.TotalListenedSeconds, h => h.TotalListenedSeconds + session.ListenedSeconds)
                        .SetProperty(h => h.QualifiedPlayCount, h => h.QualifiedPlayCount + playIncrement)
                        .SetProperty(h => h.HasFinished, h => h.HasFinished || session.PlaybackFinished)
                        .SetProperty(h => h.MaxPositionReachedSeconds, h =>
                            session.EndPositionSeconds > h.MaxPositionReachedSeconds
                                ? session.EndPositionSeconds
                                : h.MaxPositionReachedSeconds)
                        .SetProperty(h => h.PositionSeconds, h =>
                            closedAt >= h.LastAccessedAt ? session.EndPositionSeconds : h.PositionSeconds)
                        .SetProperty(h => h.LastAccessedAt, h =>
                            closedAt >= h.LastAccessedAt ? closedAt : h.LastAccessedAt)
                        .SetProperty(h => h.LastPlayedAt, h =>
                            session.IsQualifiedPlay && (!h.LastPlayedAt.HasValue || closedAt > h.LastPlayedAt)
                                ? closedAt
                                : h.LastPlayedAt)
                        .SetProperty(h => h.UpdatedAt, closedAt), ct);

                if (updated != 1)
                    throw new StreamHistoryNotFoundException(session.StreamHistoryId);
            }

            var history = await GetByIdAndUserIdAsync(session.StreamHistoryId, userId, ct)
                          ?? throw new StreamHistoryNotFoundException(session.StreamHistoryId);

            await transaction.CommitAsync(ct);

            return history;
        });
    }

    public async Task<StreamSession?> GetSessionByIdAsync(Guid sessionId, CancellationToken ct = default)
        => await _sessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.DeletedAt == null, ct);

    public async Task<PaginatedResult<StreamSessionDto>> GetSessionsAsync(Guid streamHistoryId,
        int page = 1, int pageSize = 25,
        CancellationToken ct = default)
    {
        var query = _sessions
            .Where(s => s.StreamHistoryId == streamHistoryId && s.DeletedAt == null)
            .OrderBy(s => s.StartedAt)
            .Select(s => StreamSessionDto.FromEntity(s));


        var totalCount = await query.CountAsync(ct);

        var items = await query.Skip((page - 1) * pageSize)
            .Take(page * pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<StreamSessionDto>
        {
            Items = items,
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<IReadOnlyList<ListeningSessionRow>> GetListeningSessionRowsAsync(
        Guid userId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        return await _sessions
            .AsNoTracking()
            .Where(s => s.StreamHistory.UserId == userId
                        && s.StartedAt >= fromUtc
                        && s.StartedAt < toUtc
                        && s.EndedAt != null
                        && s.ListenedSeconds > 0
                        && s.StreamHistory.File.MimeType.StartsWith("audio/")
                        && s.DeletedAt == null
                        && s.StreamHistory.DeletedAt == null
                        && s.StreamHistory.File.DeletedAt == null)
            .OrderBy(s => s.StartedAt).ThenBy(s => s.Id)
            .Select(s => new ListeningSessionRow(
                s.Id,
                s.StreamHistory.FileId,
                s.StreamHistory.File.Name,
                s.StreamHistory.File.MediaMetadata != null && s.StreamHistory.File.MediaMetadata.DeletedAt == null
                    ? s.StreamHistory.File.MediaMetadata.Title
                    : null,
                s.StreamHistory.File.MediaMetadata != null && s.StreamHistory.File.MediaMetadata.DeletedAt == null
                    ? s.StreamHistory.File.MediaMetadata.Artist
                    : null,
                s.StartedAt,
                s.ListenedSeconds,
                s.PlaybackFinished,
                s.StreamHistory.File.MediaMetadata != null && s.StreamHistory.File.MediaMetadata.DeletedAt == null
                    ? s.StreamHistory.File.MediaMetadata.Duration
                    : (double?)null,
                s.IsQualifiedPlay))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ListeningHistoryRef>> GetListeningHistoryRefsAsync(
        Guid userId, CancellationToken ct = default, DateTime? beforeUtc = null)
    {
        var cutoff = beforeUtc ?? DateTime.UtcNow;
        // Materialize only the three history fields. Record construction, grouping and
        // conditional aggregates run in memory, outside EF's SQL translation boundary.
        var rows = await ListeningHistoryRowsQuery(userId).ToListAsync(ct);
        return BuildListeningHistoryRefs(rows, cutoff);
    }

    internal IQueryable<ListeningHistorySessionRow> ListeningHistoryRowsQuery(Guid userId)
    {
        return _sessions
            .AsNoTracking()
            .Where(s => s.StreamHistory.UserId == userId && s.DeletedAt == null
                                                         && s.EndedAt != null && s.ListenedSeconds > 0
                                                         && s.StreamHistory.DeletedAt == null &&
                                                         s.StreamHistory.File.DeletedAt == null
                                                         && s.StreamHistory.File.MimeType.StartsWith("audio/"))
            .Select(s => new ListeningHistorySessionRow(s.StreamHistory.FileId,
                s.StreamHistory.File.MediaMetadata != null && s.StreamHistory.File.MediaMetadata.DeletedAt == null
                    ? s.StreamHistory.File.MediaMetadata.Artist
                    : null,
                s.StartedAt));
    }

    internal static IReadOnlyList<ListeningHistoryRef> BuildListeningHistoryRefs(
        IEnumerable<ListeningHistorySessionRow> rows, DateTime cutoff)
    {
        return rows.GroupBy(s => new { s.FileId, s.Artist })
            .Select(g => new ListeningHistoryRef(g.Key.FileId, g.Key.Artist,
                g.Min(s => s.StartedAt),
                g.Where(s => s.StartedAt < cutoff).Select(s => (DateTime?)s.StartedAt).Max()))
            .OrderBy(h => h.HistoryCreatedAt).ThenBy(h => h.FileId)
            .ToList();
    }

    internal sealed record ListeningHistorySessionRow(Guid FileId, string? Artist, DateTime StartedAt);

    public async Task<IReadOnlyList<ShuffleListenRow>> GetShuffleListenRowsAsync(
        Guid userId, IReadOnlyList<Guid> sourceFileIds, DateTime asOfUtc,
        CancellationToken ct = default)
    {
        if (sourceFileIds.Count == 0)
            return [];

        return await ShuffleListenRowsQuery(userId, sourceFileIds, asOfUtc).ToListAsync(ct);
    }

    internal IQueryable<ShuffleListenRow> ShuffleListenRowsQuery(
        Guid userId, IReadOnlyList<Guid> fileIds, DateTime asOfUtc)
    {
        return _sessions
            .AsNoTracking()
            .Where(s => s.StreamHistory.UserId == userId
                        && fileIds.Contains(s.StreamHistory.FileId)
                        && s.EndedAt != null
                        && s.ListenedSeconds > 0
                        && s.EndedAt <= asOfUtc
                        && s.DeletedAt == null
                        && s.StreamHistory.DeletedAt == null
                        && s.StreamHistory.File.DeletedAt == null
                        && s.StreamHistory.File.OwnerId == userId)
            .Select(s => new ShuffleListenRow
            {
                FileId = s.StreamHistory.FileId,
                ListenedSeconds = s.ListenedSeconds,
                EndedAtUtc = s.EndedAt!.Value,
            });
    }

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
