using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Dto.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class OutboxMessageRepository(AlexandriaDbContext context) : IOutboxMessageRepository
{
    private readonly DbSet<OutboxMessage> _messages = context.OutboxMessages;

    public async Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _messages.FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<OutboxMessage?> FirstOrDefaultAsync(Expression<Func<OutboxMessage, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _messages.FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IEnumerable<OutboxMessage>> FindAsync(Expression<Func<OutboxMessage, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _messages.Where(predicate).ToListAsync(ct);
    }

    // Never saves: the row has to be written by the caller's transaction, together with the
    // change it announces.
    public async Task<OutboxMessage> AddAsync(OutboxMessage entity, CancellationToken ct = default)
    {
        var entry = await _messages.AddAsync(entity, ct);

        return entry.Entity;
    }

    public async Task<IEnumerable<OutboxMessage>> AddRangeAsync(IEnumerable<OutboxMessage> entities,
        CancellationToken ct = default)
    {
        var messages = entities.ToList();

        await _messages.AddRangeAsync(messages, ct);

        return messages;
    }

    public void Update(OutboxMessage entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _messages.Update(entity);
    }

    public void Remove(OutboxMessage entity)
    {
        _messages.Remove(entity);
    }

    public void RemoveRange(IEnumerable<OutboxMessage> entities)
    {
        _messages.RemoveRange(entities);
    }

    public async Task<int> CountAsync(Expression<Func<OutboxMessage, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        return predicate is null
            ? await _messages.CountAsync(ct)
            : await _messages.CountAsync(predicate, ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<OutboxMessage, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _messages.AnyAsync(predicate, ct);
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(DateTime now, int batchSize,
        CancellationToken ct = default)
    {
        // SKIP LOCKED lets every host run a relay over the same table: each one takes rows the
        // others are not holding, and a relay that dies releases its rows with its connection.
        return await _messages
            .FromSqlInterpolated($"""
                SELECT * FROM "OutboxMessages"
                WHERE "SentAt" IS NULL AND "DeadAt" IS NULL AND "AvailableAt" <= {now}
                ORDER BY "AvailableAt", "CreatedAt"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);
    }

    public async Task<OutboxSummaryDto> GetSummaryAsync(int recentDeadLimit, CancellationToken ct = default)
    {
        var pending = _messages.Where(m => m.SentAt == null && m.DeadAt == null);

        var pendingCount = await pending.CountAsync(ct);
        var oldestPendingAt = await pending.MinAsync(m => (DateTime?)m.CreatedAt, ct);
        var deadCount = await _messages.CountAsync(m => m.DeadAt != null, ct);

        var recentDead = await _messages
            .AsNoTracking()
            .Where(m => m.DeadAt != null)
            .OrderByDescending(m => m.DeadAt)
            .Take(recentDeadLimit)
            .Select(m => new OutboxDeadMessageDto
            {
                Id = m.Id,
                RoutingKey = m.RoutingKey,
                Attempts = m.Attempts,
                LastError = m.LastError,
                CreatedAt = m.CreatedAt,
                DeadAt = m.DeadAt!.Value
            })
            .ToListAsync(ct);

        return new OutboxSummaryDto
        {
            PendingCount = pendingCount,
            OldestPendingAt = oldestPendingAt,
            DeadCount = deadCount,
            RecentDead = recentDead
        };
    }

    public async Task<DateTime?> GetOldestPendingCreatedAtAsync(CancellationToken ct = default)
    {
        return await _messages
            .Where(m => m.SentAt == null && m.DeadAt == null)
            .MinAsync(m => (DateTime?)m.CreatedAt, ct);
    }

    public async Task<int> ResendDeadAsync(DateTime now, CancellationToken ct = default)
    {
        return await _messages
            .Where(m => m.DeadAt != null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.DeadAt, (DateTime?)null)
                .SetProperty(m => m.Attempts, 0)
                .SetProperty(m => m.AvailableAt, now)
                // Backlog age is measured from CreatedAt, so a resent message would otherwise look
                // as old as its first send and raise a backlog incident straight away.
                .SetProperty(m => m.CreatedAt, now)
                .SetProperty(m => m.UpdatedAt, now), ct);
    }

    public async Task<int> DeleteSentBeforeAsync(DateTime sentBefore, CancellationToken ct = default)
    {
        return await _messages
            .Where(m => m.SentAt != null && m.SentAt < sentBefore)
            .ExecuteDeleteAsync(ct);
    }
}
