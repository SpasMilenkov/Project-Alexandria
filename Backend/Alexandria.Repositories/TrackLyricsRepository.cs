using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Dto.Files.Streaming.Lyrics;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class TrackLyricsRepository(AlexandriaDbContext context) : ITrackLyricsRepository
{
    private readonly DbSet<TrackLyrics> _lyrics = context.TrackLyrics;

    public async Task<TrackLyrics?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _lyrics.FindAsync([id], ct);

    public async Task<TrackLyrics?> FirstOrDefaultAsync(Expression<Func<TrackLyrics, bool>> predicate,
        CancellationToken ct = default) => await _lyrics.FirstOrDefaultAsync(predicate, ct);

    public async Task<IEnumerable<TrackLyrics>> FindAsync(Expression<Func<TrackLyrics, bool>> predicate,
        CancellationToken ct = default) => await _lyrics.Where(predicate).ToListAsync(ct);

    public async Task<TrackLyrics> AddAsync(TrackLyrics entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        var res = await _lyrics.AddAsync(entity, ct);
        return res.Entity;
    }

    public async Task<IEnumerable<TrackLyrics>> AddRangeAsync(IEnumerable<TrackLyrics> entities,
        CancellationToken ct = default)
    {
        var lyrics = entities.ToList();
        var date = DateTime.UtcNow;

        foreach (var trackLyrics in lyrics)
            trackLyrics.CreatedAt = date;

        await _lyrics.AddRangeAsync(lyrics, ct);

        return lyrics;
    }

    public void Update(TrackLyrics entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _lyrics.Update(entity);
    }

    public void Remove(TrackLyrics entity) => _lyrics.Remove(entity);

    public void RemoveRange(IEnumerable<TrackLyrics> entities) => _lyrics.RemoveRange(entities);

    public async Task<int> CountAsync(Expression<Func<TrackLyrics, bool>>? predicate = null,
        CancellationToken ct = default) => predicate == null
        ? await _lyrics.CountAsync(ct)
        : await _lyrics.Where(predicate).CountAsync(ct);


    public async Task<bool>
        ExistsAsync(Expression<Func<TrackLyrics, bool>> predicate, CancellationToken ct = default) =>
        await _lyrics.FirstOrDefaultAsync(predicate, ct) != null;

    public async Task<LyricsSearchParams?> GetSearchParamsAsync(Guid lyricsId, CancellationToken ct = default)
    {
        return await _lyrics.Where(l => l.Id == lyricsId && l.DeletedAt == null).Select(l => new LyricsSearchParams
        {
            Name = l.TranspilationJob!.FileVersion.File.MediaMetadata!.Title,
            AlbumName = l.TranspilationJob!.FileVersion.File.MediaMetadata!.Album,
            Artist = l.TranspilationJob!.FileVersion.File.MediaMetadata!.Artist,
            Duration = (int)Math.Round(l.TranspilationJob!.FileVersion.File.MediaMetadata!.Duration)
        }).FirstOrDefaultAsync(ct);
    }
}