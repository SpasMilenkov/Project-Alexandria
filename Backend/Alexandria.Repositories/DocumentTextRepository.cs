using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.DocumentTextStats;
using Alexandria.Dto.Files;
using Alexandria.Repositories.Search;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Alexandria.Repositories;

public class DocumentTextRepository(AlexandriaDbContext context) : IDocumentTextRepository
{
    // Postgres reports a search vector over its 1 MB limit with this code.
    private const string ProgramLimitExceeded = "54000";

    private const int MaxContentFragments = 3;

    // The stretch around the first word that the first-match query cuts excerpts from. An excerpt
    // runs to 24 words, so this leaves room for its context on both sides.
    private const int MatchWindowLead = 300;
    private const int MatchWindowLength = 800;

    public Task<DocumentText?> GetByContentObjectIdAsync(Guid contentObjectId, CancellationToken ct = default)
        => LiveRows().AsNoTracking().FirstOrDefaultAsync(t => t.ContentObjectId == contentObjectId, ct);

    // NOTE: excerpt queries run without a concurrency limit, and each one holds a whole document
    // in Postgres memory: about 30 MB through the first-match path and about 80 MB through the
    // full pass, for a text at the 5 M character cap. Every search row in view asks at once, so
    // many large documents on one page, or several users searching together, add up. If Postgres
    // memory ever becomes a problem, cap these queries with a process-wide SemaphoreSlim (around
    // 4) so extra requests wait their turn instead of running together.
    public async Task<ContentMatchesResponse> GetContentMatchesAsync(Guid contentObjectId, string query,
        CancellationToken ct = default)
    {
        var terms = SearchTerms.Parse(query);

        // Without a content word there is nothing to look for, but the status still tells the
        // client whether the document can be searched at all.
        var rows = terms.SearchesContent
            ? await HeadlineRows(contentObjectId, terms.ContentTsQuery).ToListAsync(ct)
            : await StatusRows(contentObjectId).ToListAsync(ct);

        var row = rows.FirstOrDefault();
        if (row is null)
            return new ContentMatchesResponse();

        return new ContentMatchesResponse
        {
            Status = (DocumentTextStatus)row.Status,
            IsTruncated = row.IsTruncated,
            Fragments = row.Headline is null ? [] : ContentMatchParser.Parse(row.Headline)
        };
    }

    public async Task<ContentMatchesResponse> GetFirstContentMatchAsync(Guid contentObjectId, string query,
        CancellationToken ct = default)
    {
        var terms = SearchTerms.Parse(query);

        if (!terms.SearchesContent)
            return await GetContentMatchesAsync(contentObjectId, query, ct);

        var rows = await WindowRows(contentObjectId, terms).ToListAsync(ct);

        var row = rows.FirstOrDefault();
        if (row is null)
            return new ContentMatchesResponse();

        var fragments = row.Headline is null ? [] : ContentMatchParser.Parse(row.Headline);

        // The regex can land on a word that Postgres reads as part of a URL or path, and a window
        // never holds words that only appear far apart. The text did match, so the full pass will
        // find what the window missed.
        if (row.IsMatch && fragments.Count == 0)
        {
            var full = await GetContentMatchesAsync(contentObjectId, query, ct);
            return new ContentMatchesResponse
            {
                Status = full.Status,
                IsTruncated = full.IsTruncated,
                Fragments = full.Fragments.Take(1).ToList()
            };
        }

        return new ContentMatchesResponse
        {
            Status = (DocumentTextStatus)row.Status,
            IsTruncated = row.IsTruncated,
            Fragments = fragments.Take(1).ToList()
        };
    }

    private IQueryable<DocumentTextWindowRow> WindowRows(Guid contentObjectId, SearchTerms terms)
    {
        var options = ContentMatchParser.HeadlineOptions(MaxContentFragments);
        var tsQuery = terms.ContentTsQuery;
        var pattern = terms.ContentWordStartPattern;

        // Each reference to a stored column reads and decompresses it again, so the concatenation
        // reads the text into memory once for both the regex and the substring, and OFFSET 0 keeps
        // the planner from inlining it back. It is only read when the stored vector matches.
        return context.Database.SqlQuery<DocumentTextWindowRow>($"""
                                                                 SELECT t."Status", t."IsTruncated", b.body IS NOT NULL AS "IsMatch",
                                                                     (SELECT ts_headline('simple',
                                                                                 substr(b.body, greatest(x.pos - {MatchWindowLead}, 1), {MatchWindowLength}),
                                                                                 to_tsquery('simple', {tsQuery}), {options})
                                                                      FROM (SELECT regexp_instr(b.body, {pattern}, 1, 1, 0, 'i') AS pos) AS x
                                                                      WHERE x.pos > 0) AS "Headline"
                                                                 FROM "DocumentText" AS t
                                                                 CROSS JOIN LATERAL (
                                                                     SELECT CASE WHEN t."SearchVector" @@ to_tsquery('simple', {tsQuery})
                                                                         THEN t."Content" || ''
                                                                     END AS body
                                                                     OFFSET 0) AS b
                                                                 WHERE t."ContentObjectId" = {contentObjectId} AND t."DeletedAt" IS NULL
                                                                 """);
    }

    private IQueryable<DocumentTextHeadlineRow> HeadlineRows(Guid contentObjectId, string tsQuery)
    {
        var options = ContentMatchParser.HeadlineOptions(MaxContentFragments);

        // The match check first, because cutting excerpts reads the whole text even when
        // nothing in it matches.
        return context.Database.SqlQuery<DocumentTextHeadlineRow>($"""
                                                                   SELECT t."Status", t."IsTruncated",
                                                                       CASE WHEN t."SearchVector" @@ to_tsquery('simple', {tsQuery})
                                                                           THEN ts_headline('simple', t."Content", to_tsquery('simple', {tsQuery}), {options})
                                                                       END AS "Headline"
                                                                   FROM "DocumentText" AS t
                                                                   WHERE t."ContentObjectId" = {contentObjectId} AND t."DeletedAt" IS NULL
                                                                   """);
    }

    private IQueryable<DocumentTextHeadlineRow> StatusRows(Guid contentObjectId)
        => context.Database.SqlQuery<DocumentTextHeadlineRow>($"""
                                                               SELECT t."Status", t."IsTruncated", NULL::text AS "Headline"
                                                               FROM "DocumentText" AS t
                                                               WHERE t."ContentObjectId" = {contentObjectId} AND t."DeletedAt" IS NULL
                                                               """);

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

    // Stays on the row only when the worker never finished the attempt; any outcome replaces it.
    internal const string InterruptedAttemptDetail =
        "Reading stopped before it finished. The worker may have run out of memory or been restarted while reading this document.";

    public Task BeginAttemptAsync(Guid contentObjectId, int extractorVersion, CancellationToken ct = default)
        => RecordFailureAsync(contentObjectId, extractorVersion, InterruptedAttemptDetail, ct);

    public Task FailAttemptAsync(Guid contentObjectId, string errorDetail, CancellationToken ct = default)
    {
        var failed = DocumentTextStatus.Failed;
        var detail = Truncate(errorDetail, ValidationConstants.StringLengths.LongString);
        DateTime? now = DateTime.UtcNow;

        return LiveRows()
            .Where(t => t.ContentObjectId == contentObjectId && t.Status == failed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.ErrorDetail, detail)
                .SetProperty(t => t.UpdatedAt, now), ct);
    }

    public Task AbandonAttemptAsync(Guid contentObjectId, CancellationToken ct = default)
    {
        var failed = DocumentTextStatus.Failed;
        DateTime? now = DateTime.UtcNow;

        // The cleared last attempt lets the next cycle pick the document up without waiting out
        // the retry delay, since the attempt never really happened.
        return LiveRows()
            .Where(t => t.ContentObjectId == contentObjectId && t.Status == failed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.AttemptCount, t => Math.Max(t.AttemptCount - 1, 0))
                .SetProperty(t => t.LastAttemptAt, (DateTime?)null)
                .SetProperty(t => t.ErrorDetail, (string?)null)
                .SetProperty(t => t.UpdatedAt, now), ct);
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

    // Rows of content the sweep can read, so the admin counts and resets cover what it works on.
    private IQueryable<DocumentText> ReadableRows(IReadOnlyCollection<string> mimeTypes)
    {
        var readable = ReadableVersions(mimeTypes);

        return LiveRows().Where(t => readable.Any(v => v.ContentObjectId == t.ContentObjectId));
    }

    public async Task<IReadOnlyList<DocumentTextStatusCount>> GetStatusCountsAsync(
        IReadOnlyCollection<string> mimeTypes, CancellationToken ct = default)
        => await ReadableRows(mimeTypes)
            .AsNoTracking()
            .GroupBy(t => t.Status)
            .OrderBy(g => g.Key)
            .Select(g => new DocumentTextStatusCount(g.Key, g.Count()))
            .ToListAsync(ct);

    public Task<int> CountWaitingAsync(IReadOnlyCollection<string> mimeTypes, int extractorVersion,
        CancellationToken ct = default)
        => ReadableVersions(mimeTypes)
            .Where(v => !context.DocumentText.Any(t =>
                t.ContentObjectId == v.ContentObjectId && t.ExtractorVersion >= extractorVersion))
            .Select(v => v.ContentObjectId)
            .Distinct()
            .CountAsync(ct);

    public async Task<IReadOnlyList<DocumentTextFailure>> GetRecentFailuresAsync(Guid viewerId,
        IReadOnlyCollection<string> mimeTypes, int maxAttempts, int limit, CancellationToken ct = default)
    {
        var failed = DocumentTextStatus.Failed;

        // Copies share content, so the name and type come from the viewer's newest version holding
        // it. The error message can quote storage keys or parts of the document, so it follows the
        // same rule as the name and stays hidden for content only other accounts hold.
        return await ReadableRows(mimeTypes)
            .AsNoTracking()
            .Where(t => t.Status == failed)
            .OrderByDescending(t => t.LastAttemptAt)
            .ThenBy(t => t.Id)
            .Take(limit)
            .Select(t => new DocumentTextFailure(
                t.ContentObjectId,
                context.FileVersions
                    .Where(v => v.ContentObjectId == t.ContentObjectId && v.File.OwnerId == viewerId)
                    .OrderByDescending(v => v.CreatedAt)
                    .Select(v => v.File.Name)
                    .FirstOrDefault(),
                context.FileVersions
                    .Where(v => v.ContentObjectId == t.ContentObjectId && v.File.OwnerId == viewerId)
                    .OrderByDescending(v => v.CreatedAt)
                    .Select(v => v.MimeType)
                    .FirstOrDefault(),
                context.FileVersions
                    .Any(v => v.ContentObjectId == t.ContentObjectId && v.File.OwnerId == viewerId)
                    ? t.ErrorDetail
                    : null,
                t.AttemptCount,
                t.LastAttemptAt,
                t.AttemptCount < maxAttempts))
            .ToListAsync(ct);
    }

    public Task<int> ResetFailedAsync(IReadOnlyCollection<string> mimeTypes, CancellationToken ct = default)
    {
        var failed = DocumentTextStatus.Failed;
        DateTime? now = DateTime.UtcNow;

        return ReadableRows(mimeTypes)
            .Where(t => t.Status == failed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.AttemptCount, 0)
                .SetProperty(t => t.LastAttemptAt, (DateTime?)null)
                .SetProperty(t => t.UpdatedAt, now), ct);
    }

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