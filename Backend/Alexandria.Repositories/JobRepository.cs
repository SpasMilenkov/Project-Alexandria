using System.Linq.Expressions;
using Alexandria.Common.Policies;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Jobs;
using Alexandria.Dto.TranspilationStats;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class JobRepository(AlexandriaDbContext context) : IJobRepository
{
    private readonly DbSet<Job> _jobs = context.Jobs;

    public async Task<Job?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _jobs.FirstOrDefaultAsync(j => j.Id == id, ct);

    public async Task<Job?> FirstOrDefaultAsync(
        Expression<Func<Job, bool>> predicate, CancellationToken ct = default)
        => await _jobs.FirstOrDefaultAsync(predicate, ct);

    public async Task<IEnumerable<Job>> FindAsync(
        Expression<Func<Job, bool>> predicate, CancellationToken ct = default)
        => await _jobs.Where(predicate).ToListAsync(ct);

    public async Task<Job> AddAsync(Job entity, CancellationToken ct = default)
    {
        var result = await _jobs.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<IEnumerable<Job>> AddRangeAsync(
        IEnumerable<Job> entities, CancellationToken ct = default)
    {
        var list = entities.ToList();
        await _jobs.AddRangeAsync(list, ct);
        await context.SaveChangesAsync(ct);
        return list;
    }

    public void Update(Job entity) => _jobs.Update(entity);

    public void Remove(Job entity) => _jobs.Remove(entity);

    public void RemoveRange(IEnumerable<Job> entities) => _jobs.RemoveRange(entities);

    public async Task<int> CountAsync(
        Expression<Func<Job, bool>>? predicate = null, CancellationToken ct = default)
    {
        var q = _jobs.AsQueryable();
        if (predicate is not null)
            q = q.Where(predicate);
        return await q.CountAsync(ct);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<Job, bool>> predicate, CancellationToken ct = default)
        => await _jobs.AnyAsync(predicate, ct);

    public async Task<bool> TryClaimJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var claimedFirstAttempt = await context.Set<Job>()
            .Where(j => j.Id == jobId && j.DeletedAt == null && j.Status == JobStatus.Queued)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, JobStatus.Processing)
                .SetProperty(j => j.StartedAt, now)
                .SetProperty(j => j.UpdatedAt, now), ct);

        if (claimedFirstAttempt > 0)
            return true;

        var claimedRetry = await context.Set<Job>()
            .Where(j => j.Id == jobId
                        && j.DeletedAt == null
                        && j.Status == JobStatus.Failed
                        && j.RetryCount < JobRetryPolicy.MaxAutoRetries)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, JobStatus.Processing)
                .SetProperty(j => j.RetryCount, j => j.RetryCount + 1)
                .SetProperty(j => j.ErrorDetail, (string?)null)
                .SetProperty(j => j.ProgressPercent, 0)
                .SetProperty(j => j.CompletedAt, (DateTime?)null)
                .SetProperty(j => j.StartedAt, now)
                .SetProperty(j => j.UpdatedAt, now), ct);

        return claimedRetry > 0;
    }

    public async Task<JobStatus> GetStatusAsync(Guid jobId, CancellationToken ct = default)
        => await _jobs.Where(j => j.Id == jobId).Select(j => j.Status).FirstAsync(ct);

    public async Task UpdateStatusAsync(
        Guid jobId,
        JobStatus status,
        int? progress = null,
        string? errorDetail = null,
        CancellationToken ct = default)
        => await _jobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, status)
                    .SetProperty(j => j.ProgressPercent, j => progress ?? j.ProgressPercent)
                    .SetProperty(j => j.ErrorDetail, j => errorDetail ?? j.ErrorDetail)
                    .SetProperty(j => j.StartedAt, j =>
                        status == JobStatus.Processing ? DateTime.UtcNow : j.StartedAt)
                    .SetProperty(j => j.CompletedAt, j =>
                        status == JobStatus.Ready || status == JobStatus.Failed || status == JobStatus.Partial
                            ? DateTime.UtcNow
                            : j.CompletedAt)
                    .SetProperty(j => j.UpdatedAt, DateTime.UtcNow),
                ct);

    public async Task ClearErrorAsync(Guid jobId, CancellationToken ct = default)
        => await _jobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.ErrorDetail, j => null), ct);

    public async Task<IReadOnlyList<Job>> GetStalledJobsAsync(
        TimeSpan threshold, JobType? type = null, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - threshold;
        return await _jobs
            .AsNoTracking()
            .Where(j => j.DeletedAt == null
                        && j.Status == JobStatus.Processing
                        && (type == null || j.Type == type)
                        && (j.UpdatedAt ?? j.StartedAt ?? j.CreatedAt) < cutoff)
            .ToListAsync(ct);
    }

    public async Task<int> FailStalledJobsAsync(
        JobType type, DateTime heartbeatBefore, string errorDetail, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _jobs
            .Where(j => j.DeletedAt == null
                        && j.Type == type
                        && j.Status == JobStatus.Processing
                        && (j.UpdatedAt ?? j.StartedAt ?? j.CreatedAt) < heartbeatBefore)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, JobStatus.Failed)
                .SetProperty(j => j.ErrorDetail, errorDetail)
                .SetProperty(j => j.CompletedAt, now)
                .SetProperty(j => j.UpdatedAt, now), ct);
    }

    public async Task<bool> ReleaseClaimAsync(Guid jobId, CancellationToken ct = default)
    {
        var released = await _jobs
            .Where(j => j.Id == jobId && j.Status == JobStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, JobStatus.Queued)
                .SetProperty(j => j.ProgressPercent, 0)
                .SetProperty(j => j.UpdatedAt, DateTime.UtcNow), ct);

        return released > 0;
    }

    public async Task<bool> TryRequeueForRetryAsync(Guid jobId, CancellationToken ct = default)
    {
        var requeued = await _jobs
            .Where(j => j.Id == jobId
                        && j.DeletedAt == null
                        && j.Status == JobStatus.Failed
                        && j.RetryCount < JobRetryPolicy.MaxAutoRetries)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, JobStatus.Queued)
                .SetProperty(j => j.RetryCount, j => j.RetryCount + 1)
                .SetProperty(j => j.ErrorDetail, (string?)null)
                .SetProperty(j => j.ProgressPercent, 0)
                .SetProperty(j => j.CompletedAt, (DateTime?)null)
                .SetProperty(j => j.UpdatedAt, DateTime.UtcNow), ct);

        return requeued > 0;
    }

    public async Task<bool> TryResetForManualRequeueAsync(
        Guid jobId, DateTime stalledBefore, CancellationToken ct = default)
    {
        var reset = await _jobs
            .Where(j => j.Id == jobId
                        && j.DeletedAt == null
                        && (j.Status == JobStatus.Failed
                            || j.Status == JobStatus.Cancelled
                            || (j.Status == JobStatus.Processing
                                && (j.UpdatedAt ?? j.StartedAt ?? j.CreatedAt) < stalledBefore)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, JobStatus.Queued)
                .SetProperty(j => j.RetryCount, 0)
                .SetProperty(j => j.ErrorDetail, (string?)null)
                .SetProperty(j => j.ProgressPercent, 0)
                .SetProperty(j => j.CompletedAt, (DateTime?)null)
                .SetProperty(j => j.UpdatedAt, DateTime.UtcNow), ct);

        return reset > 0;
    }

    public async Task MarkNonRetryableAsync(Guid jobId, CancellationToken ct = default)
        => await _jobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.RetryCount, JobRetryPolicy.MaxAutoRetries), ct);

    public async Task<IReadOnlyList<Job>> GetRetryCandidatesAsync(
        IReadOnlyCollection<JobType> types, DateTime failedBefore, int limit, CancellationToken ct = default)
        => await _jobs
            .AsNoTracking()
            .Where(j => j.DeletedAt == null
                        && types.Contains(j.Type)
                        && j.Status == JobStatus.Failed
                        && j.RetryCount < JobRetryPolicy.MaxAutoRetries
                        && j.CompletedAt != null
                        && j.CompletedAt < failedBefore)
            .OrderBy(j => j.CompletedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Job>> GetOrphanedQueuedJobsAsync(
        IReadOnlyCollection<JobType> types, DateTime queuedBefore, int limit, CancellationToken ct = default)
        => await _jobs
            .AsNoTracking()
            .Where(j => j.DeletedAt == null
                        && types.Contains(j.Type)
                        && j.Status == JobStatus.Queued
                        && (j.UpdatedAt ?? j.CreatedAt) < queuedBefore)
            .OrderBy(j => j.UpdatedAt ?? j.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task TouchAsync(Guid jobId, CancellationToken ct = default)
        => await _jobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(j => j.UpdatedAt, DateTime.UtcNow), ct);

    public async Task<IReadOnlyList<JobStatusCount>> GetStatusCountsAsync(
        JobType? type = null, CancellationToken ct = default)
        => await _jobs
            .AsNoTracking()
            .Where(j => type == null || j.Type == type)
            .GroupBy(j => j.Status)
            .Select(g => new JobStatusCount(g.Key, g.Count()))
            .ToListAsync(ct);

    public async Task<(int Failures, int Total)> GetOutcomeCountsSinceAsync(
        JobType type, DateTime since, CancellationToken ct = default)
    {
        var counts = await context.Set<Job>()
            .Where(j =>
                j.Type == type
                && j.DeletedAt == null
                && j.CompletedAt != null
                && j.CompletedAt >= since
                && (j.Status == JobStatus.Ready || j.Status == JobStatus.Failed))
            .GroupBy(j => j.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var failures = counts.FirstOrDefault(c => c.Key == JobStatus.Failed)?.Count ?? 0;
        var total = counts.Sum(c => c.Count);
        return (failures, total);
    }

    public async Task<PaginatedResult<Job>> FindJobsAsync(JobQuery query, CancellationToken ct = default)
    {
        var jobsQuery = context.Set<Job>().Where(j => j.DeletedAt == null);

        if (query.Status.HasValue)
            jobsQuery = jobsQuery.Where(j => j.Status == query.Status.Value);

        if (query.Type.HasValue)
            jobsQuery = jobsQuery.Where(j => j.Type == query.Type.Value);

        if (query.TriggeredByUserId.HasValue)
            jobsQuery = jobsQuery.Where(j => j.UserId == query.TriggeredByUserId.Value);

        if (query.MaxRetryCount.HasValue)
            jobsQuery = jobsQuery.Where(j => j.RetryCount < query.MaxRetryCount.Value);

        var totalCount = await jobsQuery.CountAsync(ct);

        var items = await jobsQuery
            .OrderBy(j => j.UpdatedAt ?? j.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PaginatedResult<Job>
        {
            Items = items,
            CurrentPage = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
        };
    }

    public async Task UpdateStatusForJobsAsync(
        IEnumerable<Guid> jobIds, JobStatus status, string? errorDetail = null, CancellationToken ct = default)
    {
        var ids = jobIds as ICollection<Guid> ?? jobIds.ToList();
        if (ids.Count == 0) return;

        var now = DateTime.UtcNow;
        await context.Jobs
            .Where(j => ids.Contains(j.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, status)
                .SetProperty(j => j.ErrorDetail, errorDetail)
                .SetProperty(j => j.CompletedAt, now)
                .SetProperty(j => j.UpdatedAt, now), ct);
    }

    public async Task<IReadOnlyList<Job>> GetJobsTouchingWindowAsync(DateTime from, DateTime to, CancellationToken ct,
        JobType? type = null)
    {
        return await _jobs
            .AsNoTracking()
            .Where(j => (type == null || j.Type == type)
                        && ((j.CreatedAt >= from && j.CreatedAt < to)
                            || (j.CompletedAt != null && j.CompletedAt >= from && j.CompletedAt < to)))
            .ToListAsync(ct);
    }
}