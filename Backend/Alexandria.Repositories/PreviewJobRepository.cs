using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class PreviewJobRepository(AlexandriaDbContext context) : IPreviewJobRepository
{
    private readonly DbSet<PreviewJob> _jobs = context.PreviewJobs;

    public async Task<PreviewJob?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _jobs
            .Include(j => j.Job)
            .FirstOrDefaultAsync(j => j.Id == id, ct);

    public async Task<PreviewJob?> GetByJobIdAsync(Guid jobId, CancellationToken ct = default)
        => await _jobs
            .Include(j => j.Job)
            .FirstOrDefaultAsync(j => j.JobId == jobId, ct);

    public async Task<PreviewJob?> GetActiveJobForVersionAsync(
        Guid versionId, PreviewKind kind, Guid userId, CancellationToken ct = default)
        => await _jobs
            .Include(j => j.Job)
            .FirstOrDefaultAsync(j =>
                j.VersionId == versionId
                && j.Kind == kind
                && j.UserId == userId
                && j.DeletedAt == null
                && (j.Job.Status == JobStatus.Queued || j.Job.Status == JobStatus.Processing), ct);

    public async Task<PreviewJob?> FirstOrDefaultAsync(
        Expression<Func<PreviewJob, bool>> predicate,
        CancellationToken ct = default)
        => await _jobs
            .Include(j => j.Job)
            .FirstOrDefaultAsync(predicate, ct);

    public async Task<IEnumerable<PreviewJob>> FindAsync(
        Expression<Func<PreviewJob, bool>> predicate,
        CancellationToken ct = default)
        => await _jobs
            .Include(j => j.Job)
            .Where(predicate)
            .ToListAsync(ct);

    public async Task<PreviewJob> AddAsync(PreviewJob entity, CancellationToken ct = default)
    {
        var result = await _jobs.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<IEnumerable<PreviewJob>> AddRangeAsync(
        IEnumerable<PreviewJob> entities,
        CancellationToken ct = default)
    {
        var list = entities.ToList();
        await _jobs.AddRangeAsync(list, ct);
        await context.SaveChangesAsync(ct);
        return list;
    }

    public void Update(PreviewJob entity) => _jobs.Update(entity);

    public void Remove(PreviewJob entity) => _jobs.Remove(entity);

    public void RemoveRange(IEnumerable<PreviewJob> entities) => _jobs.RemoveRange(entities);

    public async Task<int> CountAsync(
        Expression<Func<PreviewJob, bool>>? predicate = null, CancellationToken ct = default)
    {
        var q = _jobs.AsQueryable();
        if (predicate is not null)
            q = q.Where(predicate);
        return await q.CountAsync(ct);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<PreviewJob, bool>> predicate, CancellationToken ct = default)
        => await _jobs.AnyAsync(predicate, ct);
}