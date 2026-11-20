using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Alexandria.Repositories;

public class DocumentTextRepository(AlexandriaDbContext context) : IDocumentTextRepository
{
    // Postgres reports a search vector over its 1 MB limit with this code.
    private const string ProgramLimitExceeded = "54000";

    public Task<DocumentText?> GetByContentObjectIdAsync(Guid contentObjectId, CancellationToken ct = default)
        => LiveRows().AsNoTracking().FirstOrDefaultAsync(t => t.ContentObjectId == contentObjectId, ct);

    public async Task<bool> UpsertAsync(DocumentText text, CancellationToken ct = default)
    {
        try
        {
            await UpsertAsync(text, indexedCharacters: null, ct);
            return text.IsTruncated;
        }
        catch (PostgresException ex) when (ex.SqlState == ProgramLimitExceeded)
        {
            await UpsertAsync(text, DocumentTextExtractor.FallbackIndexedCharacters, ct);
            return true;
        }
    }

    private async Task UpsertAsync(DocumentText text, int? indexedCharacters, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var isTruncated = text.IsTruncated || indexedCharacters is not null;
        var indexLength = indexedCharacters ?? int.MaxValue;

        // The text can run to megabytes, so it is sent once and read twice through the subquery.
        await context.Database.ExecuteSqlInterpolatedAsync($"""
                                                            INSERT INTO "DocumentText" (
                                                                "Id", "ContentObjectId", "Status", "Content", "SearchVector",
                                                                "CharacterCount", "IsTruncated", "ErrorDetail", "AttemptCount", "LastAttemptAt",
                                                                "ExtractorVersion", "CreatedAt")
                                                            SELECT
                                                                {text.Id}, {text.ContentObjectId}, {(int)text.Status}, input.body,
                                                                to_tsvector('simple', left(input.body, {indexLength})),
                                                                {text.CharacterCount}, {isTruncated}, NULL, 0, {now},
                                                                {text.ExtractorVersion}, {now}
                                                            FROM (SELECT {text.Content}::text AS body) AS input
                                                            ON CONFLICT ("ContentObjectId") DO UPDATE SET
                                                                "Status" = EXCLUDED."Status",
                                                                "Content" = EXCLUDED."Content",
                                                                "SearchVector" = EXCLUDED."SearchVector",
                                                                "CharacterCount" = EXCLUDED."CharacterCount",
                                                                "IsTruncated" = EXCLUDED."IsTruncated",
                                                                "ErrorDetail" = NULL,
                                                                "AttemptCount" = 0,
                                                                "LastAttemptAt" = EXCLUDED."LastAttemptAt",
                                                                "ExtractorVersion" = EXCLUDED."ExtractorVersion",
                                                                "DeletedAt" = NULL,
                                                                "UpdatedAt" = {now}
                                                            """, ct);
    }

    public async Task RecordFailureAsync(Guid contentObjectId, int extractorVersion, string errorDetail,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var failed = (int)DocumentTextStatus.Failed;
        var detail = Truncate(errorDetail, ValidationConstants.StringLengths.LongString);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
                                                            INSERT INTO "DocumentText" (
                                                                "Id", "ContentObjectId", "Status", "Content", "SearchVector",
                                                                "CharacterCount", "IsTruncated", "ErrorDetail", "AttemptCount", "LastAttemptAt",
                                                                "ExtractorVersion", "CreatedAt")
                                                            VALUES (
                                                                {Guid.NewGuid()}, {contentObjectId}, {failed}, '', ''::tsvector,
                                                                0, FALSE, {detail}, 1, {now},
                                                                {extractorVersion}, {now})
                                                            ON CONFLICT ("ContentObjectId") DO UPDATE SET
                                                                "Status" = EXCLUDED."Status",
                                                                "ErrorDetail" = EXCLUDED."ErrorDetail",
                                                                "AttemptCount" = CASE
                                                                    WHEN "DocumentText"."Status" = {failed}
                                                                        AND "DocumentText"."ExtractorVersion" >= EXCLUDED."ExtractorVersion"
                                                                    THEN "DocumentText"."AttemptCount" + 1
                                                                    ELSE 1
                                                                END,
                                                                "LastAttemptAt" = EXCLUDED."LastAttemptAt",
                                                                "ExtractorVersion" = EXCLUDED."ExtractorVersion",
                                                                "UpdatedAt" = {now}
                                                            """, ct);
    }

    public async Task<IReadOnlyList<DocumentTextCandidate>> GetExtractionCandidatesAsync(
        IReadOnlyCollection<string> mimeTypes,
        int extractorVersion,
        DateTime retryFailedBefore,
        int maxAttempts,
        int limit,
        CancellationToken ct = default)
    {
        var failed = DocumentTextStatus.Failed;

        // Copies share content, so the batch is picked per content object first, newest upload first.
        var contentIds = await ReadableVersions(mimeTypes)
            .Where(v => !context.DocumentText.Any(t =>
                t.ContentObjectId == v.ContentObjectId
                && t.ExtractorVersion >= extractorVersion
                && (t.Status != failed
                    || t.AttemptCount >= maxAttempts
                    || t.LastAttemptAt > retryFailedBefore)))
            .GroupBy(v => v.ContentObjectId)
            .Select(g => new { ContentObjectId = g.Key, NewestAt = g.Max(v => v.CreatedAt) })
            .OrderByDescending(g => g.NewestAt)
            .Take(limit)
            .Select(g => g.ContentObjectId)
            .ToListAsync(ct);

        if (contentIds.Count == 0)
            return [];

        var versions = await ReadableVersions(mimeTypes)
            .Where(v => contentIds.Contains(v.ContentObjectId))
            .Select(v => new { v.ContentObjectId, v.Id, v.MimeType, v.CreatedAt })
            .ToListAsync(ct);

        var newestByContent = versions
            .GroupBy(v => v.ContentObjectId)
            .ToDictionary(g => g.Key, g => g.MaxBy(v => v.CreatedAt)!);

        return contentIds
            .Where(newestByContent.ContainsKey)
            .Select(id => newestByContent[id])
            .Select(v => new DocumentTextCandidate(v.ContentObjectId, v.Id, v.MimeType))
            .ToList();
    }

    private IQueryable<FileVersion> ReadableVersions(IReadOnlyCollection<string> mimeTypes)
        => context.FileVersions
            .AsNoTracking()
            .Where(v => v.DeletedAt == null
                        && !v.IsEncrypted
                        && mimeTypes.Contains(v.MimeType)
                        && v.File.DeletedAt == null
                        && v.ContentObject.IsPromoted
                        && v.ContentObject.DeletedAt == null);

    public Task<DocumentText?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => LiveRows().FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<DocumentText?> FirstOrDefaultAsync(Expression<Func<DocumentText, bool>> predicate,
        CancellationToken ct = default)
        => LiveRows().FirstOrDefaultAsync(predicate, ct);

    public async Task<IEnumerable<DocumentText>> FindAsync(Expression<Func<DocumentText, bool>> predicate,
        CancellationToken ct = default)
        => await LiveRows().Where(predicate).ToListAsync(ct);

    public async Task<DocumentText> AddAsync(DocumentText entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;

        var entry = await context.DocumentText.AddAsync(entity, ct);

        return entry.Entity;
    }

    public async Task<IEnumerable<DocumentText>> AddRangeAsync(IEnumerable<DocumentText> entities,
        CancellationToken ct = default)
    {
        var rows = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var row in rows)
            row.CreatedAt = now;

        await context.DocumentText.AddRangeAsync(rows, ct);

        return rows;
    }

    public void Update(DocumentText entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        context.DocumentText.Update(entity);
    }

    public void Remove(DocumentText entity)
        => context.DocumentText.Remove(entity);

    public void RemoveRange(IEnumerable<DocumentText> entities)
        => context.DocumentText.RemoveRange(entities);

    public Task<int> CountAsync(Expression<Func<DocumentText, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        var rows = LiveRows();

        if (predicate is not null)
            rows = rows.Where(predicate);

        return rows.CountAsync(ct);
    }

    public Task<bool> ExistsAsync(Expression<Func<DocumentText, bool>> predicate, CancellationToken ct = default)
        => LiveRows().AnyAsync(predicate, ct);

    private IQueryable<DocumentText> LiveRows()
        => context.DocumentText.Where(t => t.DeletedAt == null);

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}