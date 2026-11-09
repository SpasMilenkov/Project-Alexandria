using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Gallery;
using Microsoft.EntityFrameworkCore;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Repositories;

public sealed class GalleryRepository(AlexandriaDbContext context) : IGalleryRepository
{
    private readonly DbSet<File> _files = context.Files;

    public Task<File?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => GalleryPhotos().FirstOrDefaultAsync(f => f.Id == id, ct);

    public Task<File?> FirstOrDefaultAsync(Expression<Func<File, bool>> predicate, CancellationToken ct = default)
        => GalleryPhotos().FirstOrDefaultAsync(predicate, ct);

    public async Task<IEnumerable<File>> FindAsync(Expression<Func<File, bool>> predicate,
        CancellationToken ct = default)
        => await GalleryPhotos().Where(predicate).ToListAsync(ct);

    public async Task<File> AddAsync(File entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;

        var entry = await _files.AddAsync(entity, ct);

        return entry.Entity;
    }

    public async Task<IEnumerable<File>> AddRangeAsync(IEnumerable<File> entities, CancellationToken ct = default)
    {
        var files = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var file in files)
            file.CreatedAt = now;

        await _files.AddRangeAsync(files, ct);

        return files;
    }

    public void Update(File entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _files.Update(entity);
    }

    // Files are soft deleted everywhere so the trash can restore them; the gallery follows suit.
    public void Remove(File entity)
    {
        var now = DateTime.UtcNow;

        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        _files.Update(entity);
    }

    public void RemoveRange(IEnumerable<File> entities)
    {
        var files = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var file in files)
        {
            file.DeletedAt = now;
            file.UpdatedAt = now;
        }

        _files.UpdateRange(files);
    }

    public Task<int> CountAsync(Expression<Func<File, bool>>? predicate = null, CancellationToken ct = default)
    {
        var photos = GalleryPhotos();

        if (predicate is not null)
            photos = photos.Where(predicate);

        return photos.CountAsync(ct);
    }

    public Task<bool> ExistsAsync(Expression<Func<File, bool>> predicate, CancellationToken ct = default)
        => GalleryPhotos().AnyAsync(predicate, ct);

    public async Task<GalleryPage> GetPageAsync(GalleryPageQuery query, CancellationToken ct = default)
    {
        var photos = GalleryPhotos()
            .AsNoTracking()
            .Where(f => f.OwnerId == query.OwnerId);

        var keyed = query.Sort == GallerySort.Uploaded
            ? photos.Select(f => new { File = f, SortDate = f.CreatedAt })
            : photos.Select(f => new { File = f, SortDate = f.GalleryDate!.Value });

        if (query.Cursor is { } cursor)
        {
            // A row-value comparison, (date, id) < (cursorDate, cursorId), is one range condition
            // on the composite index. The expanded "date < x OR (date = x AND id < y)" form is
            // equivalent, but Postgres cannot turn the OR into a single index range.
            keyed = keyed.Where(k => EF.Functions.LessThan(
                ValueTuple.Create(k.SortDate, k.File.Id),
                ValueTuple.Create(cursor.SortDate, cursor.FileId)));
        }
        else if (query.Before is { } before)
        {
            keyed = keyed.Where(k => k.SortDate < before);
        }

        if (query.Since is { } since)
            keyed = keyed.Where(k => k.SortDate >= since);

        var rows = await (
                from k in keyed
                join c in context.CaptureMetadata on k.File.CurrentVersionId equals (Guid?)c.FileVersionId into captures
                from c in captures.DefaultIfEmpty()
                orderby k.SortDate descending, k.File.Id descending
                select new GalleryPhotoRow
                {
                    FileId = k.File.Id,
                    VersionId = k.File.CurrentVersionId!.Value,
                    Name = k.File.Name,
                    MimeType = k.File.MimeType,
                    DirectoryId = k.File.DirectoryId,
                    SortDate = k.SortDate,
                    TakenAt = k.File.GalleryDate!.Value,
                    UploadedAt = k.File.CreatedAt,
                    Width = c == null ? null : c.Width,
                    Height = c == null ? null : c.Height,
                })
            .Take(query.Limit + 1)
            .ToListAsync(ct);

        if (rows.Count <= query.Limit)
            return new GalleryPage { Items = rows, Next = null };

        rows.RemoveAt(rows.Count - 1);
        var last = rows[^1];

        return new GalleryPage
        {
            Items = rows,
            Next = new GalleryCursor { SortDate = last.SortDate, FileId = last.FileId },
        };
    }

    public async Task<IReadOnlyList<GalleryMonthBucketRow>> GetMonthBucketsAsync(
        Guid ownerId,
        GallerySort sort,
        string timeZoneId,
        CancellationToken ct = default)
    {
        var photos = GalleryPhotos()
            .AsNoTracking()
            .Where(f => f.OwnerId == ownerId);

        var dates = sort == GallerySort.Uploaded
            ? photos.Select(f => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(f.CreatedAt, timeZoneId))
            : photos.Select(f => f.GalleryDate!.Value);

        // Grouped in SQL: a large library is tens of thousands of rows, and the scrubber only
        // needs one small row per month.
        return await dates
            .GroupBy(d => new { d.Year, d.Month })
            .Select(g => new GalleryMonthBucketRow { Year = g.Key.Year, Month = g.Key.Month, Count = g.Count() })
            .OrderByDescending(b => b.Year)
            .ThenByDescending(b => b.Month)
            .ToListAsync(ct);
    }

    // Matches the filter of both gallery indexes, which Postgres needs to consider them.
    private IQueryable<File> GalleryPhotos()
        => _files.Where(f => f.GalleryDate != null && f.DeletedAt == null);
}