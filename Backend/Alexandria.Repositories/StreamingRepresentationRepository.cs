using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class StreamingRepresentationRepository(AlexandriaDbContext context)
    : IStreamingRepresentationRepository
{
    private readonly DbSet<StreamingRepresentation> _representations =
        context.StreamingRepresentations;

    public async Task<StreamingRepresentation?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
        => await _representations.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<StreamingRepresentation?> FirstOrDefaultAsync(
        Expression<Func<StreamingRepresentation, bool>> predicate,
        CancellationToken ct = default)
        => await _representations.FirstOrDefaultAsync(predicate, ct);

    public async Task<IEnumerable<StreamingRepresentation>> FindAsync(
        Expression<Func<StreamingRepresentation, bool>> predicate,
        CancellationToken ct = default)
        => await _representations.Where(predicate).ToListAsync(ct);

    public async Task<StreamingRepresentation> AddAsync(
        StreamingRepresentation entity,
        CancellationToken ct = default)
    {
        var result = await _representations.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<IEnumerable<StreamingRepresentation>> AddRangeAsync(
        IEnumerable<StreamingRepresentation> entities,
        CancellationToken ct = default)
    {
        var list = entities.ToList();
        await _representations.AddRangeAsync(list, ct);
        await context.SaveChangesAsync(ct);
        return list;
    }

    public void Update(StreamingRepresentation entity)
        => _representations.Update(entity);

    public void Remove(StreamingRepresentation entity)
        => _representations.Remove(entity);

    public void RemoveRange(IEnumerable<StreamingRepresentation> entities)
        => _representations.RemoveRange(entities);

    public async Task<int> CountAsync(
        Expression<Func<StreamingRepresentation, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        var q = _representations.AsQueryable();
        if (predicate is not null)
            q = q.Where(predicate);
        return await q.CountAsync(ct);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<StreamingRepresentation, bool>> predicate,
        CancellationToken ct = default)
        => await _representations.AnyAsync(predicate, ct);

    public async Task<IEnumerable<StreamingRepresentation>> GetByJobIdAsync(
        Guid jobId,
        CancellationToken ct = default)
        => await _representations
            .AsNoTracking()
            .Where(r => r.TranspilationId == jobId)
            .ToListAsync(ct);

    public async Task<PaginatedResult<StreamingRepresentation>> FindRepresentationsAsync(
        StreamingRepresentationQuery query,
        CancellationToken ct = default)
    {
        var q = _representations.AsQueryable();

        if (query.JobId.HasValue)
            q = q.Where(r => r.TranspilationId == query.JobId.Value);

        if (query.Codec.HasValue)
            q = q.Where(r => r.Codec == query.Codec.Value);

        if (query.Status.HasValue)
            q = q.Where(r => r.Status == query.Status.Value);

        if (query.MinHeight.HasValue)
            q = q.Where(r => r.Height >= query.MinHeight.Value);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .AsNoTracking()
            .OrderBy(r => r.Height)
            .ThenBy(r => r.BitrateKbps)
            .Skip(query.CurrentPage * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PaginatedResult<StreamingRepresentation>
        {
            Items = items,
            TotalCount = totalCount,
            CurrentPage = query.CurrentPage,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
        };
    }

    public async Task MarkProcessingAsync(
        Guid representationId,
        CancellationToken ct = default)
        => await _representations
            .Where(r => r.Id == representationId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, RepresentationStatus.Processing), ct);

    public async Task MarkReadyAsync(
        Guid representationId,
        CancellationToken ct = default)
        => await _representations
            .Where(r => r.Id == representationId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, RepresentationStatus.Ready)
                .SetProperty(r => r.CompletedAt, DateTime.UtcNow), ct);

    public async Task MarkFailedAsync(
        Guid representationId,
        CancellationToken ct = default)
        => await _representations
            .Where(r => r.Id == representationId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, RepresentationStatus.Failed)
                .SetProperty(r => r.CompletedAt, DateTime.UtcNow), ct);

    public async Task MarkAllProcessingAsync(
        List<Guid> representationIds,
        CancellationToken ct = default)
        => await _representations
            .Where(r => representationIds.Contains(r.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, RepresentationStatus.Processing), ct);

    public async Task MarkAllReadyAsync(
        List<Guid> representationIds,
        CancellationToken ct = default)
        => await _representations
            .Where(r => representationIds.Contains(r.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, RepresentationStatus.Ready)
                .SetProperty(r => r.CompletedAt, DateTime.UtcNow), ct);

    public async Task MarkAllFailedAsync(
        List<Guid> representationIds,
        CancellationToken ct = default)
        => await _representations
            .Where(r => representationIds.Contains(r.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, RepresentationStatus.Failed)
                .SetProperty(r => r.CompletedAt, DateTime.UtcNow), ct);

    public async Task DeleteByTranspilation(Guid transpilationId, CancellationToken ct = default)
    {
        await _representations.Where(r => r.TranspilationId == transpilationId).ExecuteDeleteAsync(ct);
    }

    public async Task<(long TotalSize, int Count)> GetStorageByUserAsync(
        Guid userId, CancellationToken ct = default)
    {
        var query = _representations
            .AsNoTracking()
            .Where(r => r.DeletedAt == null)
            .Join(context.TranspilationJobs.AsNoTracking(),
                r => r.TranspilationId, j => j.Id, (r, j) => new { Representation = r, Job = j })
            .Where(x => x.Job.UserId == userId && x.Job.DeletedAt == null);

        var totalSize = await query.SumAsync(x => (long?)x.Representation.Size, ct) ?? 0;
        var count = await query.CountAsync(ct);

        return (totalSize, count);
    }

    public async Task<Dictionary<Guid, long>> GetSizeByOwnerAsync(CancellationToken ct = default)
    {
        return await _representations
            .AsNoTracking()
            .Where(r => r.DeletedAt == null)
            .Join(context.TranspilationJobs.AsNoTracking(),
                r => r.TranspilationId, j => j.Id, (r, j) => new { Representation = r, Job = j })
            .Where(x => x.Job.DeletedAt == null)
            .GroupBy(x => x.Job.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalSize = g.Sum(x => x.Representation.Size)
            })
            .ToDictionaryAsync(x => x.UserId, x => x.TotalSize, ct);
    }

    public async Task<IReadOnlyList<StreamingRepresentation>> GetMissingSizesAsync(
        int take, CancellationToken ct = default)
    {
        return await _representations
            .AsNoTracking()
            .Include(r => r.Job)
            .ThenInclude(t => t.Job)
            .Where(r => r.Size <= 0
                        && r.DeletedAt == null
                        && r.Status == RepresentationStatus.Ready
                        && r.Job.DeletedAt == null
                        && r.Job.SegmentPrefix != null
                        && r.Job.Job.Status == JobStatus.Ready
                        && r.Job.Job.DeletedAt == null)
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<bool> TryBackfillSizeAsync(
        Guid representationId, long size, CancellationToken ct = default)
    {
        var rows = await _representations
            .Where(r => r.Id == representationId && r.Size <= 0)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Size, size), ct);
        return rows > 0;
    }
}