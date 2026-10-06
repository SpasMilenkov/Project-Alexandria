using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Common.Summaries;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.OverviewSummaries;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Alexandria.Repositories;

public class OverviewSummaryRepository(AlexandriaDbContext context) : IOverviewSummaryRepository
{
    private readonly DbSet<OverviewSummary> _summaries = context.OverviewSummaries;

    public async Task<OverviewSummary?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _summaries.FindAsync(new object[] { id }, ct);
    }

    public async Task<OverviewSummary?> FirstOrDefaultAsync(Expression<Func<OverviewSummary, bool>> predicate, CancellationToken ct = default)
    {
        return await _summaries.FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IEnumerable<OverviewSummary>> FindAsync(Expression<Func<OverviewSummary, bool>> predicate, CancellationToken ct = default)
    {
        return await _summaries.Where(predicate).ToListAsync(ct);
    }

    public async Task<OverviewSummary> AddAsync(OverviewSummary entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;

        var entry = await _summaries.AddAsync(entity, ct);

        return entry.Entity;
    }

    public async Task<IEnumerable<OverviewSummary>> AddRangeAsync(IEnumerable<OverviewSummary> entities, CancellationToken ct = default)
    {
        var list = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var entity in list)
        {
            entity.CreatedAt = now;
        }

        await _summaries.AddRangeAsync(list, ct);

        return list;
    }

    public void Update(OverviewSummary entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _summaries.Update(entity);
    }

    public void Remove(OverviewSummary entity)
    {
        _summaries.Remove(entity);
    }

    public void RemoveRange(IEnumerable<OverviewSummary> entities)
    {
        _summaries.RemoveRange(entities);
    }

    public async Task<int> CountAsync(Expression<Func<OverviewSummary, bool>>? predicate = null, CancellationToken ct = default)
    {
        if (predicate == null)
        {
            return await _summaries.CountAsync(ct);
        }

        return await _summaries.CountAsync(predicate, ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<OverviewSummary, bool>> predicate, CancellationToken ct = default)
    {
        return await _summaries.AnyAsync(predicate, ct);
    }

    public Task<OverviewSummary?> GetByPeriodAsync(
        Guid userId, SummaryKind kind, SummaryPeriod period, CancellationToken ct = default) =>
        ByKey(userId, kind, period.Start, period.End)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<OverviewSummaryHeaderDto>> ListHeadersAsync(
        Guid userId, SummaryKind kind, DateTime? from, DateTime? to, bool? finalOnly,
        CancellationToken ct = default)
    {
        var query = _summaries
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Kind == kind && x.DeletedAt == null);

        if (from.HasValue)
        {
            query = query.Where(x => x.PeriodStart >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.PeriodEnd <= to.Value);
        }

        if (finalOnly == true)
        {
            query = query.Where(x => x.FinalizedAt != null);
        }
        else if (finalOnly == false)
        {
            query = query.Where(x => x.FinalizedAt == null);
        }

        return await query
            .OrderByDescending(x => x.PeriodStart)
            .Select(x => new OverviewSummaryHeaderDto
            {
                Id = x.Id,
                Kind = x.Kind,
                PeriodStart = x.PeriodStart,
                PeriodEnd = x.PeriodEnd,
                GeneratedAt = x.GeneratedAt,
                FinalizedAt = x.FinalizedAt,
                IsFinal = x.FinalizedAt != null,
                SchemaVersion = x.SchemaVersion
            })
            .ToListAsync(ct);
    }

    public async Task<UpsertOutcome> UpsertAsync(OverviewSummary candidate, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        if (await TryUpdateProvisionalAsync(candidate, now, ct))
        {
            return UpsertOutcome.Updated;
        }

        var isFrozen = await ByKey(candidate.UserId, candidate.Kind, candidate.PeriodStart, candidate.PeriodEnd)
            .AnyAsync(x => x.FinalizedAt != null, ct);

        if (isFrozen)
        {
            return UpsertOutcome.SkippedFinal;
        }

        if (candidate.Id == Guid.Empty)
        {
            candidate.Id = Guid.NewGuid();
        }

        candidate.CreatedAt = now;
        _summaries.Add(candidate);

        try
        {
            await context.SaveChangesAsync(ct);

            return UpsertOutcome.Inserted;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            context.Entry(candidate).State = EntityState.Detached;

            if (await TryUpdateProvisionalAsync(candidate, now, ct))
            {
                return UpsertOutcome.Updated;
            }

            return UpsertOutcome.SkippedFinal;
        }
    }

    private IQueryable<OverviewSummary> ByKey(Guid userId, SummaryKind kind, DateTime start, DateTime end) =>
        _summaries.Where(x =>
            x.UserId == userId
            && x.Kind == kind
            && x.PeriodStart == start
            && x.PeriodEnd == end
            && x.DeletedAt == null);

    public Task<int> DeleteAllProvisionalAsync(
        Guid userId, SummaryKind kind, CancellationToken ct = default) =>
        _summaries
            .Where(x => x.UserId == userId
                && x.Kind == kind
                && x.FinalizedAt == null
                && x.DeletedAt == null)
            .ExecuteDeleteAsync(ct);

    private async Task<bool> TryUpdateProvisionalAsync(OverviewSummary candidate, DateTime now, CancellationToken ct)
    {
        var affected = await ByKey(candidate.UserId, candidate.Kind, candidate.PeriodStart, candidate.PeriodEnd)
            .Where(x => x.FinalizedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.PayloadJson, candidate.PayloadJson)
                .SetProperty(x => x.SchemaVersion, candidate.SchemaVersion)
                .SetProperty(x => x.GeneratedAt, candidate.GeneratedAt)
                .SetProperty(x => x.FinalizedAt, candidate.FinalizedAt)
                .SetProperty(x => x.UpdatedAt, now), ct);

        return affected > 0;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
