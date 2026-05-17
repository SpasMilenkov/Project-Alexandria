using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class TranspilationJobRepository(AlexandriaDbContext context) : ITranspilationJobRepository
{
    private readonly DbSet<TranspilationJob> _jobs = context.TranspilationJobs;

    public async Task<TranspilationJob?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _jobs.FirstOrDefaultAsync(j => j.Id == id, ct);

    public async Task<TranspilationJob?> FirstOrDefaultAsync(
        Expression<Func<TranspilationJob, bool>> predicate,
        CancellationToken ct = default)
        => await _jobs.FirstOrDefaultAsync(predicate, ct);

    public async Task<IEnumerable<TranspilationJob>> FindAsync(
        Expression<Func<TranspilationJob, bool>> predicate,
        CancellationToken ct = default)
        => await _jobs.Where(predicate).ToListAsync(ct);

    public async Task<TranspilationJob> AddAsync(TranspilationJob entity, CancellationToken ct = default)
    {
        var result = await _jobs.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<IEnumerable<TranspilationJob>> AddRangeAsync(
        IEnumerable<TranspilationJob> entities,
        CancellationToken ct = default)
    {
        var list = entities.ToList();
        await _jobs.AddRangeAsync(list, ct);
        await context.SaveChangesAsync(ct);
        return list;
    }

    public void Update(TranspilationJob entity) => _jobs.Update(entity);

    public void Remove(TranspilationJob entity) => _jobs.Remove(entity);

    public void RemoveRange(IEnumerable<TranspilationJob> entities) => _jobs.RemoveRange(entities);

    public async Task<int> CountAsync(
        Expression<Func<TranspilationJob, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        var q = _jobs.AsQueryable();
        if (predicate is not null)
            q = q.Where(predicate);
        return await q.CountAsync(ct);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<TranspilationJob, bool>> predicate,
        CancellationToken ct = default)
        => await _jobs.AnyAsync(predicate, ct);

    public async Task<TranspilationJob?> GetByVersionId(
        Guid versionId,
        CancellationToken ct = default)
        => await _jobs.FirstOrDefaultAsync(j => j.VersionId == versionId, ct);

    public async Task<TranspilationJob?> GetByVersionId(Guid versionId, Guid userId, CancellationToken ct = default)
        => await _jobs.FirstOrDefaultAsync(j => j.VersionId == versionId && j.UserId == userId, ct);

    public async Task<TranspilationJob?> GetActiveJobForVersionAsync(
        Guid versionId,
        CancellationToken ct = default)
        => await _jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j =>
                j.VersionId == versionId &&
                (j.Status == TranspilationStatus.Queued || j.Status == TranspilationStatus.Processing), ct);


    public async Task<TranspilationJob?> GetWithRepresentationsAsync(
        Guid jobId,
        CancellationToken ct = default)
        => await _jobs
            .Include(j => j.Representations)
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);

    public async Task<PaginatedResult<TranspilationJob>> FindJobsAsync(
        TranspilationJobQuery query,
        CancellationToken ct = default)
    {
        var q = _jobs.AsQueryable();

        if (!query.IsSystem)
            q = q.Where(j => j.UserId == query.UserId);

        if (query.Status.HasValue)
            q = q.Where(j => j.Status == query.Status.Value);

        if (query.IsVideo.HasValue)
            q = q.Where(j => j.IsVideo == query.IsVideo.Value);

        if (query.VersionId.HasValue)
            q = q.Where(j => j.VersionId == query.VersionId.Value);

        if (query.CreatedAfter.HasValue)
            q = q.Where(j => j.CreatedAt >= query.CreatedAfter.Value);

        if (query.CreatedBefore.HasValue)
            q = q.Where(j => j.CreatedAt <= query.CreatedBefore.Value);

        if (query.CompletedAfter.HasValue)
            q = q.Where(j => j.CompletedAt >= query.CompletedAfter.Value);

        if (query.CompletedBefore.HasValue)
            q = q.Where(j => j.CompletedAt <= query.CompletedBefore.Value);

        if (query.MinRetryCount.HasValue)
            q = q.Where(j => j.RetryCount >= query.MinRetryCount.Value);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Skip((query.CurrentPage - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(j => j.Representations)
            .ToListAsync(ct);

        return new PaginatedResult<TranspilationJob>
        {
            Items = items,
            TotalCount = totalCount,
            CurrentPage = query.CurrentPage,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
        };
    }

    public async Task UpdateStatusAsync(
        Guid jobId,
        TranspilationStatus status,
        int? progress = null,
        string? errorDetail = null,
        string? segmentPrefix = null,
        CancellationToken ct = default)
        => await _jobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, status)
                    .SetProperty(j => j.ProgressPercent, j => progress ?? j.ProgressPercent)
                    .SetProperty(j => j.ErrorDetail, j => errorDetail ?? j.ErrorDetail)
                    .SetProperty(j => j.StartedAt, j =>
                        status == TranspilationStatus.Processing ? DateTime.UtcNow : j.StartedAt)
                    .SetProperty(j => j.SegmentPrefix, j => segmentPrefix ?? j.SegmentPrefix)
                    .SetProperty(j => j.CompletedAt, j =>
                        status == TranspilationStatus.Ready
                        || status == TranspilationStatus.Failed
                        || status == TranspilationStatus.Partial
                            ? DateTime.UtcNow
                            : j.CompletedAt),
                ct);

    public async Task<IEnumerable<TranspilationJob>> GetStalledJobsAsync(
        TimeSpan threshold,
        CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow - threshold;
        return await _jobs
            .Where(j => j.Status == TranspilationStatus.Processing && j.StartedAt < cutoff)
            .ToListAsync(ct);
    }


    public async Task<bool> TryClaimJobAsync(
        Guid jobId,
        CancellationToken ct = default)
    {
        var affected = await _jobs
            .Where(j => j.Id == jobId && j.Status == TranspilationStatus.Queued)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, TranspilationStatus.Processing)
                .SetProperty(j => j.StartedAt, DateTime.UtcNow), ct);

        return affected > 0;
    }
}