using Common.Repositories;
using Data.Context;
using DTO;
using DTO.Audit;
using DTO.Extensions;
using DTO.Files;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Enumerators;

namespace Repositories;

public class AuditLogRepository(AlexandriaDbContext context) : IAuditLogRepository
{
    private readonly DbSet<AuditLog> _dbSet = context.AuditLogs;
    public async Task AddAsync(AuditLogDto dto, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(dto.ToEntity(), ct);
    }

    public async Task<PaginatedResult<AuditLogResult>> FetchAuditLogAsync(AuditLogQuery query, CancellationToken ct = default)
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
                Description = a.Description,
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
}