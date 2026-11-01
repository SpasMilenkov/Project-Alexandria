using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Dto.Events.Operational;
using Alexandria.Dto.Files;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Alexandria.Repositories;

public class OperationalEventRepository(AlexandriaDbContext context) : IOperationalEventRepository
{
    public async Task<PaginatedResult<OperationalEvent>> GetEventsAsync(OperationalEventQuery query,
        CancellationToken ct = default)
    {
        var q = context.OperationalEvents.AsNoTracking();

        if (query.ServiceType is not null) q = q.Where(e => e.ServiceType == query.ServiceType);
        if (query.Status is not null) q = q.Where(e => e.Status == query.Status);
        if (query.Severity is not null) q = q.Where(e => e.Severity == query.Severity);
        if (query.Code is not null) q = q.Where(e => e.Code == query.Code);
        if (query.From is not null) q = q.Where(e => e.CreatedAt >= query.From);
        if (query.To is not null) q = q.Where(e => e.CreatedAt <= query.To);

        var totalCount = await q.CountAsync(ct);
        var items = await q.OrderByDescending(e => e.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PaginatedResult<OperationalEvent>
        {
            CurrentPage = query.Page,
            Items = items,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
        };
    }

    public async Task<List<EventCalendarRow>> GetCalendarRowsAsync(DateTime from, DateTime to,
        CancellationToken ct = default)
    {
        return await context.OperationalEvents
            .AsNoTracking()
            .Where(e => e.CreatedAt >= from && e.CreatedAt < to)
            .Select(e => new EventCalendarRow(e.CreatedAt, e.ServiceType, e.Severity))
            .ToListAsync(ct);
    }

    public async Task<List<DowntimeInterval>> GetDowntimeIntervalsAsync(ServiceType? service, DateTime from,
        DateTime to, CancellationToken ct = default)
    {
        var q = context.OperationalEvents
            .AsNoTracking()
            .Where(e => e.Severity == OperationalEventSeverity.Failure)
            .Where(e => e.CreatedAt < to && (e.ResolvedAt == null || e.ResolvedAt > from));

        if (service is not null)
            q = q.Where(e => e.ServiceType == service);

        return await q
            .Select(e => new DowntimeInterval(e.ServiceType, e.CreatedAt, e.ResolvedAt))
            .ToListAsync(ct);
    }

    public async Task<OperationalEvent?> GetActiveEventAsync(
        ServiceType serviceType, OperationalEventCode code, CancellationToken ct)
    {
        return await context.OperationalEvents
            .Where(e => e.ServiceType == serviceType
                        && e.Code == code
                        && e.Status == OperationalEventStatus.Active)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> ResolveActiveEventsAsync(
        ServiceType serviceType, OperationalEventCode code, DateTime resolvedAt, CancellationToken ct)
    {
        return await context.OperationalEvents
            .Where(e => e.ServiceType == serviceType
                        && e.Code == code
                        && e.Status == OperationalEventStatus.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Status, OperationalEventStatus.Resolved)
                .SetProperty(e => e.ResolvedAt, resolvedAt)
                .SetProperty(e => e.UpdatedAt, resolvedAt), ct);
    }

    public async Task<int> TouchActiveEventAsync(
        ServiceType serviceType, OperationalEventCode code, DateTime seenAt, CancellationToken ct)
    {
        return await context.OperationalEvents
            .Where(e => e.ServiceType == serviceType
                        && e.Code == code
                        && e.Status == OperationalEventStatus.Active)
            .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.LastSeenAt, seenAt), ct);
    }

    public async Task<bool> AddActiveIfAbsentAsync(OperationalEvent entity, CancellationToken ct)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.LastSeenAt ??= entity.CreatedAt;
        context.OperationalEvents.Add(entity);

        try
        {
            await context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            context.Entry(entity).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<List<OperationalEvent>> GetIdleActiveEventsAsync(
        OperationalEventCode code, DateTime idleBefore, CancellationToken ct)
    {
        return await context.OperationalEvents
            .Where(e => e.Code == code
                        && e.Status == OperationalEventStatus.Active
                        && (e.LastSeenAt ?? e.CreatedAt) < idleBefore)
            .ToListAsync(ct);
    }

    public async Task<List<OperationalEvent>> GetCurrentStatusPerServiceAsync(CancellationToken ct)
    {
        return await CurrentStatusPerServiceQuery(context.OperationalEvents.AsNoTracking()).ToListAsync(ct);
    }

    internal static IQueryable<OperationalEvent> CurrentStatusPerServiceQuery(IQueryable<OperationalEvent> events)
    {
        return events
            .Where(e => e.Status == OperationalEventStatus.Active)
            .GroupBy(e => e.ServiceType)
            .Select(g => g
                .OrderByDescending(e => e.Severity == OperationalEventSeverity.Failure)
                .ThenByDescending(e => e.Severity == OperationalEventSeverity.PartialFailure)
                .ThenByDescending(e => e.CreatedAt)
                .ThenBy(e => e.Id)
                .First());
    }

    /// <inheritdoc/>
    public async Task<OperationalEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.OperationalEvents.FindAsync(new object[] { id }, ct);
    }

    /// <inheritdoc/>
    public async Task<OperationalEvent?> FirstOrDefaultAsync(Expression<Func<OperationalEvent, bool>> predicate,
        CancellationToken ct = default)
    {
        return await context.OperationalEvents.FirstOrDefaultAsync(predicate, ct);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<OperationalEvent>> FindAsync(Expression<Func<OperationalEvent, bool>> predicate,
        CancellationToken ct = default)
    {
        return await context.OperationalEvents.Where(predicate).ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<OperationalEvent> AddAsync(OperationalEvent entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.LastSeenAt ??= entity.CreatedAt;
        var entry = await context.OperationalEvents.AddAsync(entity, ct);
        return entry.Entity;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<OperationalEvent>> AddRangeAsync(IEnumerable<OperationalEvent> entities,
        CancellationToken ct = default)
    {
        var eventList = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var entity in eventList)
        {
            entity.CreatedAt = now;
            entity.LastSeenAt ??= now;
        }

        await context.OperationalEvents.AddRangeAsync(eventList, ct);
        return eventList;
    }

    /// <inheritdoc/>
    public void Update(OperationalEvent entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        context.OperationalEvents.Update(entity);
    }

    /// <inheritdoc/>
    public void Remove(OperationalEvent entity)
    {
        context.OperationalEvents.Remove(entity);
    }

    /// <inheritdoc/>
    public void RemoveRange(IEnumerable<OperationalEvent> entities)
    {
        context.OperationalEvents.RemoveRange(entities);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(Expression<Func<OperationalEvent, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        return predicate == null
            ? await context.OperationalEvents.CountAsync(ct)
            : await context.OperationalEvents.CountAsync(predicate, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(Expression<Func<OperationalEvent, bool>> predicate,
        CancellationToken ct = default)
    {
        return await context.OperationalEvents.AnyAsync(predicate, ct);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
