using System.Runtime.CompilerServices;
using Alexandria.Common.Config;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class AuditLogRepository(AlexandriaDbContext context) : IAuditLogRepository
{
    private readonly DbSet<AuditLog> _dbSet = context.AuditLogs;

    public async Task<PaginatedResult<AuditLogResult>> FetchAuditLogAsync(
        AuditLogFilter filter,
        AuditPageRequest page,
        CancellationToken ct = default)
    {
        var filtered = ApplyFilter(_dbSet.AsNoTracking(), filter);

        var count = await filtered.CountAsync(ct);

        var items = await Sort(Project(filtered), page.SortBy, page.SortDirection)
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(ct);

        return new PaginatedResult<AuditLogResult>
        {
            CurrentPage = page.Page,
            PageSize = page.PageSize,
            Items = items,
            TotalCount = count,
            TotalPages = TotalPages(count, page.PageSize)
        };
    }

    public async IAsyncEnumerable<AuditLogResult> StreamAsync(
        AuditLogFilter filter,
        int maxRows,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var query = Sort(Project(ApplyFilter(_dbSet.AsNoTracking(), filter)), AuditSortBy.Timestamp,
                SortDirection.Desc)
            .Take(maxRows)
            .AsAsyncEnumerable()
            .WithCancellation(ct);

        await foreach (var row in query)
            yield return row;
    }

    public async Task<IReadOnlyList<AuditLogResult>> GetRecentAsync(
        AuditLogFilter filter,
        int limit,
        SortDirection order = SortDirection.Desc,
        CancellationToken ct = default)
    {
        var newest = Sort(Project(ApplyFilter(_dbSet.AsNoTracking(), filter)), AuditSortBy.Timestamp,
                SortDirection.Desc)
            .Take(limit);

        // Re-sorting after Take runs as an outer query, so the limit still keeps the newest rows.
        if (order == SortDirection.Asc)
            newest = Sort(newest, AuditSortBy.Timestamp, SortDirection.Asc);

        return await newest.ToListAsync(ct);
    }

    public async Task<int> DeleteRangeAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(a => a.Timestamp < cutoff)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<bool> AnyAsync(AuditLogFilter filter, CancellationToken ct = default)
    {
        return await ApplyFilter(_dbSet.AsNoTracking(), filter).AnyAsync(ct);
    }

    public async Task<int> CountAsync(AuditLogFilter filter, CancellationToken ct = default)
    {
        return await ApplyFilter(_dbSet.AsNoTracking(), filter).CountAsync(ct);
    }

    public async Task<IReadOnlyList<AuditCursor>> GetPositionsAsync(
        AuditLogFilter filter,
        int skip,
        int take,
        SortDirection order = SortDirection.Desc,
        CancellationToken ct = default)
    {
        var rows = ApplyFilter(_dbSet.AsNoTracking(), filter);

        var sorted = order == SortDirection.Asc
            ? rows.OrderBy(a => a.Timestamp).ThenBy(a => a.Id)
            : rows.OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id);

        return await sorted
            .Skip(skip)
            .Take(take)
            .Select(a => new AuditCursor(a.Timestamp, a.Id))
            .ToListAsync(ct);
    }

    public async Task<AuditLogResult?> GetLatestAsync(AuditEventCode eventCode, CancellationToken ct = default)
    {
        return await Project(_dbSet.AsNoTracking().Where(a => a.EventCode == eventCode))
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<AuditSignInRow>> GetSignInRowsAsync(
        AuditEventCode eventCode,
        DateTimeOffset from,
        bool includeMetadata,
        CancellationToken ct = default)
    {
        var start = from.ToUniversalTime();

        return await _dbSet.AsNoTracking()
            .Where(a => a.EventCode == eventCode && a.Timestamp >= start)
            .Select(a => new AuditSignInRow(a.OwnerId, a.IpAddress, includeMetadata ? a.MetadataJson : null,
                a.Timestamp))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditSignInHistoryRow>> GetSignInHistoryAsync(
        Guid? ownerId = null,
        DateTimeOffset? from = null,
        CancellationToken ct = default)
    {
        var rows = _dbSet.AsNoTracking().Where(a => a.EventCode == AuditEventCode.UserLogin);

        if (ownerId.HasValue)
            rows = rows.Where(a => a.OwnerId == ownerId);

        if (from.HasValue)
        {
            var start = from.Value.ToUniversalTime();
            rows = rows.Where(a => a.Timestamp >= start);
        }

        return await rows
            .GroupBy(a => new { a.OwnerId, a.IpAddress })
            .Select(g => new AuditSignInHistoryRow(g.Key.OwnerId, g.Key.IpAddress, g.Min(a => a.Timestamp),
                g.Max(a => a.Timestamp), g.Count()))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditActivityRow>> GetActivityRowsAsync(
        Guid userId,
        ActivityQuery query,
        CancellationToken ct = default)
    {
        var start = query.StartDate.ToUniversalTime();
        var end = query.EndDate.ToUniversalTime();

        var rows = _dbSet.AsNoTracking()
            .Where(a => a.UserId == userId || a.OwnerId == userId)
            .Where(a => a.Timestamp >= start && a.Timestamp <= end)
            .Where(a => a.OperationType == OperationType.Create
                        || a.OperationType == OperationType.Read
                        || a.OperationType == OperationType.Update
                        || a.OperationType == OperationType.Delete);

        if (query.EntityType.HasValue)
            rows = rows.Where(a => a.EntityType == query.EntityType);

        if (query.OperationType.HasValue)
            rows = rows.Where(a => a.OperationType == query.OperationType);

        return await rows
            .Select(a => new AuditActivityRow(a.OperationType, a.Timestamp))
            .ToListAsync(ct);
    }

    internal static int TotalPages(int count, int pageSize) =>
        pageSize <= 0 ? 0 : (count + pageSize - 1) / pageSize;

    internal static IQueryable<AuditLog> ApplyFilter(IQueryable<AuditLog> query, AuditLogFilter filter)
    {
        if (filter.ScopeUserId.HasValue)
        {
            var scope = filter.ScopeUserId.Value;
            query = query.Where(a => a.UserId == scope || a.OwnerId == scope);
        }

        if (filter.ReaderId.HasValue)
        {
            var reader = filter.ReaderId.Value;
            var contentTypes = AuditPersonalContent.EntityTypes.ToList();
            var contentEvents = AuditPersonalContent.EventCodes.ToList();

            // A missing owner on library content means it could not be resolved, not that the row
            // is public; only shared tags are ownerless by design.
            query = query.Where(a => a.UserId == reader || a.OwnerId == reader
                || (a.OwnerId == null && a.EntityType == EntityType.Tag)
                || (!contentTypes.Contains(a.EntityType) && !contentEvents.Contains(a.EventCode)));
        }

        if (filter.ExcludePersonalContent)
        {
            var contentTypes = AuditPersonalContent.EntityTypes.ToList();
            var contentEvents = AuditPersonalContent.EventCodes.ToList();

            query = query.Where(a => !contentTypes.Contains(a.EntityType) && !contentEvents.Contains(a.EventCode));
        }

        if (filter.UserId.HasValue)
            query = query.Where(a => a.UserId == filter.UserId);

        if (filter.OwnerId.HasValue)
            query = query.Where(a => a.OwnerId == filter.OwnerId);

        if (filter.EntityId.HasValue)
            query = ApplyEntityFilter(query, filter);
        else if (filter.EntityType.HasValue)
            query = query.Where(a => a.EntityType == filter.EntityType);

        if (filter.EventCodes is { Count: > 0 })
        {
            var codes = filter.EventCodes.ToList();
            query = query.Where(a => codes.Contains(a.EventCode));
        }

        if (filter.OperationType.HasValue)
            query = query.Where(a => a.OperationType == filter.OperationType);

        if (filter.Source.HasValue)
            query = query.Where(a => a.Source == filter.Source);

        if (!string.IsNullOrEmpty(filter.IpAddress))
            query = query.Where(a => a.IpAddress == filter.IpAddress);

        if (filter.From.HasValue)
        {
            var from = filter.From.Value.ToUniversalTime();
            query = query.Where(a => a.Timestamp >= from);
        }

        if (filter.To.HasValue)
        {
            var to = filter.To.Value.ToUniversalTime();
            query = query.Where(a => a.Timestamp <= to);
        }

        if (filter.CorrelationId.HasValue)
            query = query.Where(a => a.CorrelationId == filter.CorrelationId);

        // Row-value comparisons keep each cursor bound a single range condition on (Timestamp, Id).
        if (filter.UpTo is { } upTo)
        {
            var at = upTo.Timestamp.ToUniversalTime();
            var id = upTo.Id;
            query = query.Where(a => EF.Functions.LessThanOrEqual(
                ValueTuple.Create(a.Timestamp, a.Id), ValueTuple.Create(at, id)));
        }

        if (filter.After is { } after)
        {
            var at = after.Timestamp.ToUniversalTime();
            var id = after.Id;
            query = query.Where(a => EF.Functions.GreaterThan(
                ValueTuple.Create(a.Timestamp, a.Id), ValueTuple.Create(at, id)));
        }

        return query;
    }

    private static IQueryable<AuditLog> ApplyEntityFilter(IQueryable<AuditLog> query, AuditLogFilter filter)
    {
        var entityId = filter.EntityId!.Value;

        if (filter.IncludeChildren && filter.EntityType.HasValue)
        {
            var type = filter.EntityType.Value;
            return query.Where(a => (a.EntityType == type && a.EntityId == entityId) || a.ParentEntityId == entityId);
        }

        if (filter.IncludeChildren)
            return query.Where(a => a.EntityId == entityId || a.ParentEntityId == entityId);

        if (filter.EntityType.HasValue)
        {
            var type = filter.EntityType.Value;
            return query.Where(a => a.EntityType == type && a.EntityId == entityId);
        }

        return query.Where(a => a.EntityId == entityId);
    }

    private static IQueryable<AuditLogResult> Sort(IQueryable<AuditLogResult> query, AuditSortBy sortBy, SortDirection direction)
    {
        var ascending = direction == SortDirection.Asc;

        if (sortBy == AuditSortBy.OperationType)
        {
            return ascending
                ? query.OrderBy(a => a.OperationType).ThenBy(a => a.Timestamp).ThenBy(a => a.Id)
                : query.OrderByDescending(a => a.OperationType).ThenByDescending(a => a.Timestamp)
                    .ThenByDescending(a => a.Id);
        }

        return ascending
            ? query.OrderBy(a => a.Timestamp).ThenBy(a => a.Id)
            : query.OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id);
    }

    private IQueryable<AuditLogResult> Project(IQueryable<AuditLog> query)
    {
        var systemId = SystemConfig.SystemId;

        return from a in query
            join u in context.Users on a.UserId equals u.Id into actors
            from actor in actors.DefaultIfEmpty()
            select new AuditLogResult
            {
                Id = a.Id,
                OperationType = a.OperationType,
                EntityType = a.EntityType,
                UserId = a.UserId,
                UserName = a.UserId == systemId || actor == null ? null : actor.Name,
                EntityId = a.EntityId ?? Guid.Empty,
                OwnerId = a.OwnerId,
                ParentEntityId = a.ParentEntityId,
                CorrelationId = a.CorrelationId,
                EntityName = a.EntityName,
                EventCode = a.EventCode,
                FallbackDescription = a.FallbackDescription,
                Metadata = a.MetadataJson,
                IpAddress = a.IpAddress,
                Timestamp = a.Timestamp,
                LogSource = a.Source
            };
    }
}
