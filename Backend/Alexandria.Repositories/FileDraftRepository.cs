using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class FileDraftRepository(AlexandriaDbContext context) : IFileDraftRepository
{
    private readonly DbSet<FileDraft> _drafts = context.FileDrafts;

    public async Task<FileDraft?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _drafts.FirstOrDefaultAsync(d => d.FileId == id, ct);
    }

    public async Task<Guid?> GetRevisionAsync(Guid fileId, CancellationToken ct = default)
    {
        return await _drafts
            .Where(d => d.FileId == fileId)
            .Select(d => (Guid?)d.Revision)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<FileDraft?> FirstOrDefaultAsync(Expression<Func<FileDraft, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _drafts.FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IEnumerable<FileDraft>> FindAsync(Expression<Func<FileDraft, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _drafts.Where(predicate).ToListAsync(ct);
    }

    public async Task<FileDraft> AddAsync(FileDraft entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt ??= entity.CreatedAt;

        var entry = await _drafts.AddAsync(entity, ct);

        return entry.Entity;
    }

    public async Task<IEnumerable<FileDraft>> AddRangeAsync(IEnumerable<FileDraft> entities,
        CancellationToken ct = default)
    {
        var drafts = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var draft in drafts)
        {
            draft.CreatedAt = now;
            draft.UpdatedAt ??= now;
        }

        await _drafts.AddRangeAsync(drafts, ct);

        return drafts;
    }

    public void Update(FileDraft entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _drafts.Update(entity);
    }

    public void Remove(FileDraft entity)
    {
        _drafts.Remove(entity);
    }

    public void RemoveRange(IEnumerable<FileDraft> entities)
    {
        _drafts.RemoveRange(entities);
    }

    public async Task<int> CountAsync(Expression<Func<FileDraft, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        if (predicate is null) return await _drafts.CountAsync(ct);

        return await _drafts.CountAsync(predicate, ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<FileDraft, bool>> predicate, CancellationToken ct = default)
    {
        return await _drafts.AnyAsync(predicate, ct);
    }

    public async Task<IReadOnlyList<IdleFileDraft>> GetIdleAsync(DateTime writtenBefore, int limit,
        CancellationToken ct = default)
    {
        return await _drafts
            .Where(d => d.UpdatedAt < writtenBefore
                        && d.File!.DeletedAt == null
                        && d.File.CurrentVersionId == d.BaseVersionId)
            .OrderBy(d => d.UpdatedAt)
            .Take(limit)
            .Select(d => new IdleFileDraft
            {
                FileId = d.FileId,
                BaseVersionId = d.BaseVersionId,
                Revision = d.Revision,
                OwnerId = d.File!.OwnerId,
            })
            .ToListAsync(ct);
    }

    public async Task<string?> GetContentAsync(Guid fileId, Guid revision, CancellationToken ct = default)
    {
        return await _drafts
            .Where(d => d.FileId == fileId && d.Revision == revision)
            .Select(d => d.Content)
            .FirstOrDefaultAsync(ct);
    }
}
