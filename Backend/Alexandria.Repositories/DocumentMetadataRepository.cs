using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class DocumentMetadataRepository(AlexandriaDbContext context) : IDocumentMetadataRepository
{
    public Task<DocumentMetadata?> GetByVersionIdAsync(Guid versionId, CancellationToken ct = default)
        => LiveRows().AsNoTracking().FirstOrDefaultAsync(d => d.FileVersionId == versionId, ct);

    public async Task UpsertAsync(DocumentMetadata metadata, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "DocumentMetadata" (
                "Id", "FileVersionId", "Title", "Subject", "Keywords", "Language",
                "Author", "LastModifiedBy", "Company", "Application", "Producer",
                "AuthoredAt", "AuthoredOffsetMinutes", "ModifiedAt", "ModifiedOffsetMinutes",
                "PageCount", "WordCount", "SlideCount", "SheetCount", "EditingMinutes",
                "PaperSize", "IsPasswordProtected",
                "Publisher", "Isbn", "Series", "SeriesIndex", "IsDrmProtected",
                "RawJson", "ExtractorVersion", "CreatedAt")
            VALUES (
                {metadata.Id}, {metadata.FileVersionId}, {metadata.Title}, {metadata.Subject},
                {metadata.Keywords}, {metadata.Language},
                {metadata.Author}, {metadata.LastModifiedBy}, {metadata.Company},
                {metadata.Application}, {metadata.Producer},
                {metadata.AuthoredAt}, {metadata.AuthoredOffsetMinutes},
                {metadata.ModifiedAt}, {metadata.ModifiedOffsetMinutes},
                {metadata.PageCount}, {metadata.WordCount}, {metadata.SlideCount},
                {metadata.SheetCount}, {metadata.EditingMinutes},
                {metadata.PaperSize}, {metadata.IsPasswordProtected},
                {metadata.Publisher}, {metadata.Isbn}, {metadata.Series}, {metadata.SeriesIndex},
                {metadata.IsDrmProtected},
                {metadata.RawJson}::jsonb, {metadata.ExtractorVersion}, {now})
            ON CONFLICT ("FileVersionId") DO UPDATE SET
                "Title" = EXCLUDED."Title",
                "Subject" = EXCLUDED."Subject",
                "Keywords" = EXCLUDED."Keywords",
                "Language" = EXCLUDED."Language",
                "Author" = EXCLUDED."Author",
                "LastModifiedBy" = EXCLUDED."LastModifiedBy",
                "Company" = EXCLUDED."Company",
                "Application" = EXCLUDED."Application",
                "Producer" = EXCLUDED."Producer",
                "AuthoredAt" = EXCLUDED."AuthoredAt",
                "AuthoredOffsetMinutes" = EXCLUDED."AuthoredOffsetMinutes",
                "ModifiedAt" = EXCLUDED."ModifiedAt",
                "ModifiedOffsetMinutes" = EXCLUDED."ModifiedOffsetMinutes",
                "PageCount" = EXCLUDED."PageCount",
                "WordCount" = EXCLUDED."WordCount",
                "SlideCount" = EXCLUDED."SlideCount",
                "SheetCount" = EXCLUDED."SheetCount",
                "EditingMinutes" = EXCLUDED."EditingMinutes",
                "PaperSize" = EXCLUDED."PaperSize",
                "IsPasswordProtected" = EXCLUDED."IsPasswordProtected",
                "Publisher" = EXCLUDED."Publisher",
                "Isbn" = EXCLUDED."Isbn",
                "Series" = EXCLUDED."Series",
                "SeriesIndex" = EXCLUDED."SeriesIndex",
                "IsDrmProtected" = EXCLUDED."IsDrmProtected",
                "RawJson" = EXCLUDED."RawJson",
                "ExtractorVersion" = EXCLUDED."ExtractorVersion",
                "UpdatedAt" = {now}
            """, ct);
    }

    public async Task<IReadOnlyList<DocumentMetadataCandidate>> GetExtractionCandidatesAsync(
        IReadOnlyCollection<string> mimeTypes,
        int extractorVersion,
        int limit,
        IReadOnlyCollection<Guid> excludedVersionIds,
        CancellationToken ct = default)
    {
        return await context.FileVersions
            .AsNoTracking()
            .Where(v => v.DeletedAt == null
                        && !v.IsEncrypted
                        && mimeTypes.Contains(v.MimeType)
                        && v.File.DeletedAt == null
                        && v.ContentObject.IsPromoted
                        && !excludedVersionIds.Contains(v.Id)
                        && !context.DocumentMetadata.Any(d =>
                            d.FileVersionId == v.Id && d.ExtractorVersion >= extractorVersion))
            .OrderByDescending(v => v.CreatedAt)
            .Take(limit)
            .Select(v => new DocumentMetadataCandidate(v.Id, v.MimeType))
            .ToListAsync(ct);
    }

    public Task<DocumentMetadata?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => LiveRows().FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<DocumentMetadata?> FirstOrDefaultAsync(Expression<Func<DocumentMetadata, bool>> predicate,
        CancellationToken ct = default)
        => LiveRows().FirstOrDefaultAsync(predicate, ct);

    public async Task<IEnumerable<DocumentMetadata>> FindAsync(Expression<Func<DocumentMetadata, bool>> predicate,
        CancellationToken ct = default)
        => await LiveRows().Where(predicate).ToListAsync(ct);

    public async Task<DocumentMetadata> AddAsync(DocumentMetadata entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;

        var entry = await context.DocumentMetadata.AddAsync(entity, ct);

        return entry.Entity;
    }

    public async Task<IEnumerable<DocumentMetadata>> AddRangeAsync(IEnumerable<DocumentMetadata> entities,
        CancellationToken ct = default)
    {
        var rows = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var row in rows)
            row.CreatedAt = now;

        await context.DocumentMetadata.AddRangeAsync(rows, ct);

        return rows;
    }

    public void Update(DocumentMetadata entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        context.DocumentMetadata.Update(entity);
    }

    public void Remove(DocumentMetadata entity)
        => context.DocumentMetadata.Remove(entity);

    public void RemoveRange(IEnumerable<DocumentMetadata> entities)
        => context.DocumentMetadata.RemoveRange(entities);

    public Task<int> CountAsync(Expression<Func<DocumentMetadata, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        var rows = LiveRows();

        if (predicate is not null)
            rows = rows.Where(predicate);

        return rows.CountAsync(ct);
    }

    public Task<bool> ExistsAsync(Expression<Func<DocumentMetadata, bool>> predicate, CancellationToken ct = default)
        => LiveRows().AnyAsync(predicate, ct);

    private IQueryable<DocumentMetadata> LiveRows()
        => context.DocumentMetadata.Where(d => d.DeletedAt == null);
}
