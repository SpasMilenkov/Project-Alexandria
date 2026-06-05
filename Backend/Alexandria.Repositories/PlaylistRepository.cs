using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Dto.Files.Streaming.Playlist;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class PlaylistRepository(AlexandriaDbContext context) : IPlaylistRepository
{
    private readonly DbSet<Playlist> _playlists = context.Playlists;
    private readonly DbSet<PlaylistItem> _playlistItems = context.PlaylistItems;

    public async Task<Playlist?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _playlists
            .Where(p => p.DeletedAt == null)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Playlist> CreateAsync(Playlist entity, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        entity.CreatedAt = now;
        entity.UpdatedAt = null;
        entity.DeletedAt = null;

        if (entity.PlaylistItems is not null)
        {
            foreach (var item in entity.PlaylistItems)
                item.CreatedAt = now;
        }

        await _playlists.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<Playlist> UpdateAsync(Playlist entity, CancellationToken ct = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _playlists.Update(entity);
        await context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<PlaylistDto?> GetSummaryAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        return await _playlists
            .Where(p => p.Id == id && p.OwnerId == userId && p.DeletedAt == null)
            .Select(p => new PlaylistDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                HasCover = p.HasCover,
                AmbientTheme = p.AmbientTheme,
                ItemCount = p.PlaylistItems!.Count(i => i.DeletedAt == null),
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PlaylistDetailDto?> GetWithItemsAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        return await _playlists
            .Where(p => p.Id == id && p.OwnerId == userId && p.DeletedAt == null)
            .Select(p => new PlaylistDetailDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                HasCover = p.HasCover,
                AmbientTheme = p.AmbientTheme,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                Items = p.PlaylistItems!
                    .Where(i => i.DeletedAt == null)
                    .OrderBy(i => i.Position)
                    .Select(i => new PlaylistItemDto
                    {
                        Id = i.Id,
                        Position = i.Position,
                        TranspilationJobId = i.TranspilationJobId,
                        FileName = i.TranspilationJob!.FileVersion.File.Name,
                        MimeType = i.TranspilationJob!.FileVersion.File.MimeType,
                        SegmentPrefix = i.TranspilationJob!.SegmentPrefix,
                        CreatedAt = i.CreatedAt,
                        Representations = i.TranspilationJob!.Representations
                            .Where(r => r.DeletedAt == null)
                            .Select(r => new StreamingRepresentationDto
                            {
                                Id = r.Id,
                                Codec = r.Codec,
                                Width = r.Width,
                                Height = r.Height,
                                BitrateKbps = r.BitrateKbps,
                                Status = r.Status
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaginatedResult<PlaylistDto>> GetByUserAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _playlists
            .Where(p => p.OwnerId == userId && p.DeletedAt == null);

        var count = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PlaylistDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                HasCover = p.HasCover,
                AmbientTheme = p.AmbientTheme,
                ItemCount = p.PlaylistItems!.Count(i => i.DeletedAt == null),
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync(ct);

        return new PaginatedResult<PlaylistDto>
        {
            Items = items,
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = count,
            TotalPages = (int)Math.Ceiling(count / (double)pageSize)
        };
    }

    public async Task SoftDeleteAsync(Guid playlistId, Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        await _playlistItems
            .Where(i => i.PlaylistId == playlistId && i.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.DeletedAt, now)
                .SetProperty(i => i.UpdatedAt, now)
                .SetProperty(i => i.UpdatedBy, userId), ct);

        await _playlists
            .Where(p => p.Id == playlistId && p.OwnerId == userId && p.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.DeletedAt, now)
                .SetProperty(p => p.UpdatedAt, now)
                .SetProperty(p => p.UpdatedBy, userId), ct);
    }

    public async Task ReorderItemsAsync(
        Guid playlistId, Guid[] orderedItemIds, CancellationToken ct = default)
    {
        var items = await _playlistItems
            .Where(i => i.PlaylistId == playlistId && i.DeletedAt == null)
            .ToListAsync(ct);

        var positionMap = orderedItemIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);

        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            if (!positionMap.TryGetValue(item.Id, out var position))
                continue;

            item.Position = position;
            item.UpdatedAt = now;
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task<PlaylistItemDto> AddItemAsync(PlaylistItem item, CancellationToken ct = default)
    {
        var maxPosition = await _playlistItems
            .Where(i => i.PlaylistId == item.PlaylistId && i.DeletedAt == null)
            .MaxAsync(i => (int?)i.Position, ct) ?? -1;

        item.Position = maxPosition + 1;
        item.CreatedAt = DateTime.UtcNow;

        await _playlistItems.AddAsync(item, ct);
        await context.SaveChangesAsync(ct);

        return await _playlistItems
            .Where(i => i.Id == item.Id)
            .Select(i => new PlaylistItemDto
            {
                Id = i.Id,
                Position = i.Position,
                TranspilationJobId = i.TranspilationJobId,
                FileName = i.TranspilationJob!.FileVersion.File.Name,
                MimeType = i.TranspilationJob!.FileVersion.File.MimeType,
                SegmentPrefix = i.TranspilationJob!.SegmentPrefix,
                CreatedAt = i.CreatedAt,
                Representations = i.TranspilationJob!.Representations
                    .Where(r => r.DeletedAt == null)
                    .Select(r => new StreamingRepresentationDto
                    {
                        Id = r.Id,
                        Codec = r.Codec,
                        Width = r.Width,
                        Height = r.Height,
                        BitrateKbps = r.BitrateKbps,
                        Status = r.Status
                    })
                    .ToList()
            })
            .FirstAsync(ct);
    }

    public async Task RemoveItemAsync(
        Guid playlistId, Guid itemId, Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        await _playlistItems
            .Where(i => i.Id == itemId && i.PlaylistId == playlistId && i.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.DeletedAt, now)
                .SetProperty(i => i.UpdatedAt, now)
                .SetProperty(i => i.UpdatedBy, userId), ct);
    }

    public async Task<string?> GetRandomPreviewPathForJobsAsync(
        Guid[] jobIds, CancellationToken ct = default)
    {
        // Walk the chain: TranspilationJob -> FileVersion -> File -> Preview
        // The subquery resolves the file IDs without loading any entities.
        var fileIds = context.Set<TranspilationJob>()
            .Where(j => jobIds.Contains(j.Id) && j.DeletedAt == null)
            .Select(j => j.FileVersion.FileId);

        return await context.Set<Preview>()
            .Where(p => p.DeletedAt == null && fileIds.Contains(p.FileId))
            .OrderBy(_ => EF.Functions.Random())
            .Select(p => p.Path)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> IsOwnerAsync(Guid playlistId, Guid userId, CancellationToken ct = default)
    {
        return await _playlists
            .AnyAsync(p => p.Id == playlistId && p.OwnerId == userId && p.DeletedAt == null, ct);
    }

    // IRepository<Playlist> base members

    public async Task<IEnumerable<Playlist>> GetAllAsync(CancellationToken ct = default)
        => await _playlists.Where(p => p.DeletedAt == null).ToListAsync(ct);

    public async Task<IEnumerable<Playlist>> FindAsync(
        Expression<Func<Playlist, bool>> predicate, CancellationToken ct = default)
        => await _playlists.Where(p => p.DeletedAt == null).Where(predicate).ToListAsync(ct);

    public async Task<Playlist?> FirstOrDefaultAsync(
        Expression<Func<Playlist, bool>> predicate, CancellationToken ct = default)
        => await _playlists.Where(p => p.DeletedAt == null).FirstOrDefaultAsync(predicate, ct);

    public async Task<Playlist> AddAsync(Playlist entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        var result = await _playlists.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<IEnumerable<Playlist>> AddRangeAsync(
        IEnumerable<Playlist> entities, CancellationToken ct = default)
    {
        var list = entities.ToList();
        var now = DateTime.UtcNow;
        foreach (var e in list) e.CreatedAt = now;
        await _playlists.AddRangeAsync(list, ct);
        await context.SaveChangesAsync(ct);
        return list;
    }

    public void Update(Playlist entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _playlists.Update(entity);
    }

    public void Remove(Playlist entity)
    {
        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _playlists.Update(entity);
    }

    public void RemoveRange(IEnumerable<Playlist> entities)
    {
        var now = DateTime.UtcNow;
        var playlists = entities as Playlist[] ?? entities.ToArray();
        foreach (var e in playlists)
        {
            e.DeletedAt = now;
            e.UpdatedAt = now;
        }

        _playlists.UpdateRange(playlists);
    }

    public async Task<int> CountAsync(
        Expression<Func<Playlist, bool>>? predicate = null, CancellationToken ct = default)
    {
        var query = _playlists.Where(p => p.DeletedAt == null);
        if (predicate != null) query = query.Where(predicate);
        return await query.CountAsync(ct);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<Playlist, bool>> predicate, CancellationToken ct = default)
        => await _playlists.Where(p => p.DeletedAt == null).AnyAsync(predicate, ct);
}