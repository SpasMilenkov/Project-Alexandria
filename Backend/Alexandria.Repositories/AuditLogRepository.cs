using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Extensions;
using Alexandria.Dto.Files;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class AuditLogRepository(AlexandriaDbContext context) : IAuditLogRepository
{
    private readonly DbSet<AuditLog> _dbSet = context.AuditLogs;

    public async Task AddAsync(AuditLogDto dto, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(dto.ToEntity(), ct);
    }

    public async Task<PaginatedResult<AuditLogResult>> FetchAuditLogAsync(AuditLogQuery query,
        CancellationToken ct = default)
    {
        IQueryable<AuditLog> dbQuery = _dbSet.Where(a => a.UserId == query.UserId);

        if (query.After.HasValue)
        {
            dbQuery = dbQuery.Where(a => a.Timestamp >= query.After);
        }

        if (query.Before.HasValue)
        {
            dbQuery = dbQuery.Where(a => a.Timestamp <= query.Before);
        }

        if (query.EntityType.HasValue)
        {
            dbQuery = dbQuery.Where(a => a.EntityType == query.EntityType);
        }

        if (query.OperationType.HasValue)
        {
            dbQuery = dbQuery.Where(a => a.OperationType == query.OperationType);
        }

        if (query.EntityId.HasValue)
        {
            dbQuery = dbQuery.Where(a => a.EntityId == query.EntityId);
        }

        if (!string.IsNullOrEmpty(query.IpAddress))
        {
            dbQuery = dbQuery.Where(a => a.IpAddress == query.IpAddress);
        }

        dbQuery = query.SortBy switch
        {
            "timestamp" => query.SortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(a => a.Timestamp)
                : dbQuery.OrderByDescending(a => a.Timestamp),

            "operationType" => query.SortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(a => a.OperationType)
                : dbQuery.OrderByDescending(a => a.OperationType),

            _ => dbQuery.OrderBy(d => d.Timestamp)
        };

        var count = await dbQuery.CountAsync(cancellationToken: ct);

        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new AuditLogResult
            {
                OperationType = a.OperationType,
                EntityType = a.EntityType,
                UserId = a.UserId ?? Guid.Empty,
                EntityId = a.EntityId ?? Guid.Empty,
                EventCode = a.EventCode,
                FallbackDescription = a.FallbackDescription,
                Metadata = a.MetadataJson,
                IpAddress = a.IpAddress,
                Timestamp = a.Timestamp,
                LogSource = a.Source
            })
            .ToListAsync(cancellationToken: ct);


        return new PaginatedResult<AuditLogResult>
        {
            CurrentPage = query.Page,
            PageSize = query.PageSize,
            Items = items,
            TotalCount = count,
            TotalPages = count / query.PageSize
        };
    }

    public async Task DeleteRangeAsync(CancellationToken ct = default)
    {
        await _dbSet
            .Where(a => a.Timestamp <= DateTimeOffset.UtcNow.AddYears(-2))
            .ExecuteDeleteAsync(cancellationToken: ct);
    }

    public async Task<ActivityStatisticsOverview> GetOverviewAsync(Guid userId, ActivityQuery query,
        CancellationToken ct = default)
    {
        var dbQuery = _dbSet.Where(a => a.UserId == userId && a.Timestamp <= query.EndDate.ToUniversalTime() &&
                                        a.Timestamp >= query.StartDate.ToUniversalTime() &&
                                        (a.OperationType == OperationType.Create
                                         || a.OperationType == OperationType.Read
                                         || a.OperationType == OperationType.Update
                                         || a.OperationType == OperationType.Delete));

        var data = await dbQuery
            .Select(a => new { a.OperationType, a.Timestamp }).ToListAsync(ct);

        var totalCount = await dbQuery.CountAsync(ct);

        var activityPerDay = data
            .GroupBy(a => a.Timestamp.DayOfYear)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => new ActivitySummary
            {
                TotalOperations = g.Count(),
                CountPerOperation = g.GroupBy(a => a.OperationType).ToDictionary(og => og.Key, og => og.Count())
            });

        return new ActivityStatisticsOverview
        {
            TotalActivity = totalCount,
            ActivityPerDay = activityPerDay
        };
    }
}