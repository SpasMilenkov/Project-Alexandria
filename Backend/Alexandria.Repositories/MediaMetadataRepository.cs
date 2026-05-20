using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class MediaMetadataRepository(AlexandriaDbContext context) : IMediaMetadataRepository
{
    private readonly DbSet<MediaMetadata> _mediaMetadata = context.MediaMetadata;

    public async Task<MediaMetadata?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _mediaMetadata.FindAsync(new object[] { id }, ct);
    }

    public async Task<MediaMetadata?> FirstOrDefaultAsync(Expression<Func<MediaMetadata, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _mediaMetadata.FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IEnumerable<MediaMetadata>> GetAllAsync(CancellationToken ct = default)
    {
        return await _mediaMetadata.ToListAsync(ct);
    }

    public async Task<IEnumerable<MediaMetadata>> FindAsync(Expression<Func<MediaMetadata, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _mediaMetadata.Where(predicate).ToListAsync(ct);
    }

    public async Task<MediaMetadata> AddAsync(MediaMetadata entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        var entry = await _mediaMetadata.AddAsync(entity, ct);
        return entry.Entity;
    }

    public async Task<IEnumerable<MediaMetadata>> AddRangeAsync(IEnumerable<MediaMetadata> entities,
        CancellationToken ct = default)
    {
        var metadataList = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var entity in metadataList)
        {
            entity.CreatedAt = now;
        }

        await _mediaMetadata.AddRangeAsync(metadataList, ct);
        return metadataList;
    }

    public void Update(MediaMetadata entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _mediaMetadata.Update(entity);
    }

    public void Remove(MediaMetadata entity)
    {
        _mediaMetadata.Remove(entity);
    }

    public void RemoveRange(IEnumerable<MediaMetadata> entities)
    {
        _mediaMetadata.RemoveRange(entities);
    }

    public async Task<int> CountAsync(Expression<Func<MediaMetadata, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        return predicate == null
            ? await _mediaMetadata.CountAsync(ct)
            : await _mediaMetadata.CountAsync(predicate, ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<MediaMetadata, bool>> predicate, CancellationToken ct = default)
    {
        return await _mediaMetadata.AnyAsync(predicate, ct);
    }

    public async Task<MediaMetadata> CreateAsync(MediaMetadata metadata, CancellationToken ct = default)
    {
        metadata.CreatedAt = DateTime.UtcNow;
        var entry = await _mediaMetadata.AddAsync(metadata, ct);
        await context.SaveChangesAsync(ct);
        return entry.Entity;
    }

    public async Task<MediaMetadata> UpdateAsync(MediaMetadata metadata, CancellationToken ct = default)
    {
        metadata.UpdatedAt = DateTime.UtcNow;
        _mediaMetadata.Update(metadata);
        await context.SaveChangesAsync(ct);
        return metadata;
    }

    public async Task<double> GetFileDurationAsync(Guid fileId, CancellationToken ct = default)
        => await _mediaMetadata.Where(m => m.FileId == fileId).Select(m => m.Duration).FirstAsync(ct);
}