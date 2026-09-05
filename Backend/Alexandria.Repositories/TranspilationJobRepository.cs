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
        => await _jobs
            .Include(j => j.Job)
            .FirstOrDefaultAsync(j => j.Id == id, ct);

    public async Task<TranspilationJob?> GetByJobIdAsync(Guid jobId, CancellationToken ct = default)
        => await _jobs.FirstOrDefaultAsync(j => j.JobId == jobId, ct);

    public async Task<TranspilationJob?> FirstOrDefaultAsync(
        Expression<Func<TranspilationJob, bool>> predicate,
        CancellationToken ct = default)
        => await _jobs
            .Include(j => j.Job)
            .FirstOrDefaultAsync(predicate, ct);

    public async Task<IEnumerable<TranspilationJob>> FindAsync(
        Expression<Func<TranspilationJob, bool>> predicate,
        CancellationToken ct = default)
        => await _jobs
            .Include(j => j.Job)
            .Where(predicate)
            .ToListAsync(ct);

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
        => await _jobs
            .Include(j => j.Job)
            .FirstOrDefaultAsync(j => j.VersionId == versionId, ct);

    public async Task<TranspilationJob?> GetByVersionId(Guid versionId, Guid userId, CancellationToken ct = default)
        => await _jobs
            .Include(j => j.Job)
            .FirstOrDefaultAsync(j => j.VersionId == versionId && j.UserId == userId, ct);

    public async Task<PaginatedResult<TranspilationJobWithDetailsDto>> GetWithDetailsAsync(
        TranspilationJobQuery query,
        CancellationToken ct = default)
    {
        var q = ApplyFilters(query);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Skip((query.CurrentPage - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(j => new TranspilationJobWithDetailsDto
            {
                Id = j.Id,
                VersionId = j.VersionId,
                Status = j.Job.Status,
                IsVideo = j.IsVideo,
                FileName = j.FileVersion.File.Name,
                VersionNumber = j.FileVersion.VersionNumber,
                ProgressPercent = j.Job.ProgressPercent,
                RetryCount = j.Job.RetryCount,
                ErrorDetail = j.Job.ErrorDetail,
                StartedAt = j.Job.StartedAt,
                CompletedAt = j.Job.CompletedAt,
                CreatedAt = j.CreatedAt,
                AudioRungs = j.AudioRungs,
                VideoRungs = j.VideoRungs,
                Representations = j.Representations
                    .Where(r => r.DeletedAt == null)
                    .Select(r => new StreamingRepresentationDto()
                    {
                        Id = r.Id,
                        JobId = r.TranspilationId,
                        Codec = r.Codec,
                        Width = r.Width,
                        Height = r.Height,
                        BitrateKbps = r.BitrateKbps,
                        Status = r.Status,
                        CompletedAt = r.CompletedAt
                    })
                    .ToList()
            }).ToListAsync(ct);

        return new PaginatedResult<TranspilationJobWithDetailsDto>
        {
            Items = items,
            TotalCount = totalCount,
            CurrentPage = query.CurrentPage,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
        };
    }

    public async Task<TranspilationJob?> GetActiveJobForVersionAsync(
        Guid versionId,
        CancellationToken ct = default)
        => await _jobs
            .AsNoTracking()
            .Include(j => j.Job)
            .FirstOrDefaultAsync(j =>
                j.VersionId == versionId &&
                (j.Job.Status == JobStatus.Queued || j.Job.Status == JobStatus.Processing), ct);

    public async Task<TranspilationJob?> GetWithRepresentationsAsync(
        Guid jobId,
        CancellationToken ct = default)
        => await _jobs
            .Include(j => j.Job)
            .Include(j => j.Representations)
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);

    public async Task<PaginatedResult<TranspilationJob>> FindJobsAsync(
        TranspilationJobQuery query,
        CancellationToken ct = default)
    {
        var q = ApplyFilters(query);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Skip((query.CurrentPage - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(j => j.Job)
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

    // Shared filtering for GetWithDetailsAsync / FindJobsAsync. Status/RetryCount/CompletedAt
    // live on the related Job row, so these predicates go through j.Job - EF translates that
    // into a join for report/listing queries, which is a legitimate use here since the result
    // is a merged DTO, not a hot-path status check.
    private IQueryable<TranspilationJob> ApplyFilters(TranspilationJobQuery query)
    {
        var q = _jobs.AsQueryable();

        if (!query.IsSystem)
            q = q.Where(j => j.UserId == query.UserId);

        if (query.Status.HasValue)
            q = q.Where(j => j.Job.Status == query.Status.Value);

        if (query.IsVideo.HasValue)
            q = q.Where(j => j.IsVideo == query.IsVideo.Value);

        if (query.VersionId.HasValue)
            q = q.Where(j => j.VersionId == query.VersionId.Value);

        if (query.CreatedAfter.HasValue)
            q = q.Where(j => j.CreatedAt >= query.CreatedAfter.Value);

        if (query.CreatedBefore.HasValue)
            q = q.Where(j => j.CreatedAt <= query.CreatedBefore.Value);

        if (query.CompletedAfter.HasValue)
            q = q.Where(j => j.Job.CompletedAt >= query.CompletedAfter.Value);

        if (query.CompletedBefore.HasValue)
            q = q.Where(j => j.Job.CompletedAt <= query.CompletedBefore.Value);

        if (query.MinRetryCount.HasValue)
            q = q.Where(j => j.Job.RetryCount >= query.MinRetryCount.Value);

        return q;
    }

    // Rungs and segment prefix are transpilation-specific and stay here, keyed by
    // TranspilationJob.Id. Status/progress/retry/error live on Job now - see JobRepository.
    public async Task UpdateDetailsAsync(
        Guid transpilationJobId,
        string? segmentPrefix = null,
        AudioRung[]? audioRungs = null,
        VideoRung[]? videoRungs = null,
        CancellationToken ct = default)
        => await _jobs
            .Where(j => j.Id == transpilationJobId)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.AudioRungs, j => audioRungs ?? j.AudioRungs)
                    .SetProperty(j => j.VideoRungs, j => videoRungs ?? j.VideoRungs)
                    .SetProperty(j => j.SegmentPrefix, j => segmentPrefix ?? j.SegmentPrefix),
                ct);

    public async Task<IReadOnlyList<TranspilationJob>> GetJobsTouchingWindowAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await _jobs
            .AsNoTracking()
            .Include(j => j.Job)
            .Where(j => (j.CreatedAt >= from && j.CreatedAt < to)
                        || (j.Job.CompletedAt != null
                            && j.Job.CompletedAt >= from && j.Job.CompletedAt < to))
            .ToListAsync(ct);
    }
}