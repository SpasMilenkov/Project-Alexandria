using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Enrichment;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class EssentiaBatchFileRepository(AlexandriaDbContext context) : IEssentiaBatchFileRepository
{
    private readonly DbSet<EssentiaBatchFile> _batchFiles = context.EssentiaBatchFiles;

    public async Task<EssentiaBatchFile?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _batchFiles.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<EssentiaBatchFile?> FirstOrDefaultAsync(
        Expression<Func<EssentiaBatchFile, bool>> predicate,
        CancellationToken ct = default)
        => await _batchFiles.FirstOrDefaultAsync(predicate, ct);

    public async Task<IEnumerable<EssentiaBatchFile>> FindAsync(
        Expression<Func<EssentiaBatchFile, bool>> predicate,
        CancellationToken ct = default)
        => await _batchFiles.Where(predicate).ToListAsync(ct);

    public async Task<EssentiaBatchFile> AddAsync(EssentiaBatchFile entity, CancellationToken ct = default)
    {
        var result = await _batchFiles.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<IEnumerable<EssentiaBatchFile>> AddRangeAsync(
        IEnumerable<EssentiaBatchFile> entities,
        CancellationToken ct = default)
    {
        var list = entities.ToList();
        await _batchFiles.AddRangeAsync(list, ct);
        await context.SaveChangesAsync(ct);
        return list;
    }

    public void Update(EssentiaBatchFile entity) => _batchFiles.Update(entity);

    public void Remove(EssentiaBatchFile entity) => _batchFiles.Remove(entity);

    public void RemoveRange(IEnumerable<EssentiaBatchFile> entities) => _batchFiles.RemoveRange(entities);

    public async Task<int> CountAsync(
        Expression<Func<EssentiaBatchFile, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        var q = _batchFiles.AsQueryable();
        if (predicate is not null)
            q = q.Where(predicate);
        return await q.CountAsync(ct);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<EssentiaBatchFile, bool>> predicate,
        CancellationToken ct = default)
        => await _batchFiles.AnyAsync(predicate, ct);

    public async Task<IEnumerable<EssentiaBatchFile>> GetByBatchAsync(Guid batchId, CancellationToken ct = default)
        => await _batchFiles
            .Where(f => f.BatchId == batchId && f.DeletedAt == null)
            .ToListAsync(ct);

    public async Task<int> UpdateStatusForBatchAsync(
        Guid batchId,
        EssentiaBatchFileStatus currentStatus,
        EssentiaBatchFileStatus newStatus,
        Guid? updatedBy,
        CancellationToken ct = default)
        => await _batchFiles
            .Where(f => f.BatchId == batchId && f.Status == currentStatus)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.Status, newStatus)
                .SetProperty(f => f.CompletedAt, DateTime.UtcNow)
                .SetProperty(f => f.UpdatedAt, DateTime.UtcNow)
                .SetProperty(f => f.UpdatedBy, updatedBy), ct);

    public async Task<IEnumerable<Guid>> GetAutoTagBackstopCandidatesAsync(CancellationToken ct = default)
    {
        var succeeded = await _batchFiles
            .Where(f => f.DeletedAt == null
                        && f.Status == EssentiaBatchFileStatus.Succeeded
                        && f.File.DeletedAt == null)
            .Select(f => f.FileId)
            .Distinct()
            .ToListAsync(ct);

        if (succeeded.Count == 0) return [];

        var autoTagged = await context.FileTags
            .Where(ft => ft.Source == TagSource.Auto
                         && (ft.Tag.Facet == TagFacet.Genre || ft.Tag.Facet == TagFacet.Mood)
                         && succeeded.Contains(ft.FileId))
            .Select(ft => ft.FileId)
            .Distinct()
            .ToListAsync(ct);

        var autoTaggedSet = autoTagged.ToHashSet();
        return succeeded.Where(id => !autoTaggedSet.Contains(id)).ToList();
    }

    public async Task<IEnumerable<Guid>> GetAutoTagRetryCandidatesAsync(
        DateTime attemptCutoff,
        CancellationToken ct = default)
    {
        var retryStatuses = new[]
        {
            EssentiaBatchFileStatus.Failed,
            EssentiaBatchFileStatus.MissingOutput,
        };

        // A file is a retry candidate when its newest attempt (any outcome) is a failed /
        // missing-output row older than the cutoff, and the file's current version is not
        // client-encrypted. The NOT EXISTS guard makes "newest" correct across attempts:
        // a newer Succeeded or Pending row disqualifies the file entirely.
        return await _batchFiles
            .Where(f => f.DeletedAt == null
                        && retryStatuses.Contains(f.Status)
                        && f.UpdatedAt != null
                        && f.UpdatedAt < attemptCutoff
                        && f.File.DeletedAt == null
                        && (f.File.CurrentVersion == null || !f.File.CurrentVersion.IsEncrypted)
                        && !_batchFiles.Any(other => other.DeletedAt == null
                                                     && other.FileId == f.FileId
                                                     && (other.UpdatedAt ?? other.CreatedAt) >
                                                     (f.UpdatedAt ?? f.CreatedAt)))
            .Select(f => f.FileId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EnrichmentQueueDepthDto>> GetQueueDepthAsync(CancellationToken ct = default)
    {
        return await _batchFiles
            .Where(f => f.DeletedAt == null && f.Batch.Status == EssentiaBatchStatus.Dispatched)
            .GroupBy(f => new { Backbone = f.Batch.Backbone.ToString(), Status = f.Status.ToString() })
            .Select(g => new EnrichmentQueueDepthDto
            {
                Backbone = g.Key.Backbone,
                Status = g.Key.Status,
                Count = g.Count(),
            })
            .OrderBy(d => d.Backbone)
            .ThenBy(d => d.Status)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EnrichmentStuckDto>> GetStuckAsync(DateTime olderThan,
        CancellationToken ct = default)
    {
        return await _batchFiles
            .Where(f => f.DeletedAt == null
                        && f.Status == EssentiaBatchFileStatus.Pending
                        && f.CreatedAt < olderThan
                        && f.Batch.Status == EssentiaBatchStatus.Dispatched)
            .Select(f => new EnrichmentStuckDto
            {
                FileId = f.FileId,
                BatchId = f.BatchId,
                Backbone = f.Batch.Backbone.ToString(),
                CreatedAt = f.CreatedAt,
            })
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EnrichmentFailureRatePointDto>> GetFailureRateAsync(
        DateTime from,
        DateTime to,
        EnrichmentBucket bucket,
        CancellationToken ct = default)
    {
        var datePart = bucket == EnrichmentBucket.Day ? "day" : "hour";

        return await context.Database.SqlQuery<EnrichmentFailureRatePointDto>($"""
                                                                               SELECT date_trunc({datePart}, f."CreatedAt") AS "Bucket",
                                                                                      b."Backbone" AS "Backbone",
                                                                                      COUNT(*) FILTER (WHERE f."Status" = 'Failed' OR f."Status" = 'MissingOutput')::int AS "Failed",
                                                                                      COUNT(*) FILTER (WHERE f."Status" = 'Succeeded')::int AS "Succeeded",
                                                                                      COUNT(*)::int AS "Total"
                                                                               FROM "EssentiaBatchFiles" f
                                                                               JOIN "EssentiaBatches" b ON b."Id" = f."BatchId"
                                                                               WHERE f."DeletedAt" IS NULL
                                                                                 AND f."CreatedAt" >= {from}
                                                                                 AND f."CreatedAt" < {to}
                                                                                 AND (f."Status" = 'Succeeded' OR f."Status" = 'Failed' OR f."Status" = 'MissingOutput')
                                                                               GROUP BY 1, 2
                                                                               ORDER BY "Bucket", "Backbone"
                                                                               """)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EnrichmentDurationDto>> GetDurationAsync(CancellationToken ct = default)
    {
        return await context.Database.SqlQuery<EnrichmentDurationDto>($"""
                                                                       SELECT b."Backbone" AS "Backbone",
                                                                              AVG(EXTRACT(EPOCH FROM (f."CompletedAt" - f."CreatedAt"))::double precision) AS "AverageSeconds",
                                                                              PERCENTILE_CONT(0.5) WITHIN GROUP
                                                                                  (ORDER BY EXTRACT(EPOCH FROM (f."CompletedAt" - f."CreatedAt"))::double precision) AS "MedianSeconds",
                                                                              MAX(EXTRACT(EPOCH FROM (f."CompletedAt" - f."CreatedAt"))::double precision) AS "MaxSeconds"
                                                                       FROM "EssentiaBatchFiles" f
                                                                       JOIN "EssentiaBatches" b ON b."Id" = f."BatchId"
                                                                       WHERE f."CompletedAt" IS NOT NULL
                                                                       GROUP BY b."Backbone"
                                                                       ORDER BY b."Backbone"
                                                                       """)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EnrichmentVolumePointDto>> GetVolumeByHourAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        return await context.Database.SqlQuery<EnrichmentVolumePointDto>($"""
                                                                          SELECT date_trunc('hour', "CreatedAt") AS "Hour",
                                                                                 COUNT(*)::int AS "Count"
                                                                          FROM "EssentiaBatchFiles"
                                                                          WHERE "DeletedAt" IS NULL
                                                                            AND "CreatedAt" >= {from}
                                                                            AND "CreatedAt" < {to}
                                                                          GROUP BY date_trunc('hour', "CreatedAt")
                                                                          ORDER BY "Hour"
                                                                          """)
            .ToListAsync(ct);
    }
}