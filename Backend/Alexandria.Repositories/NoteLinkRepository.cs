using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Links;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Alexandria.Repositories;

public class NoteLinkRepository(AlexandriaDbContext context) : INoteLinkRepository
{
    private readonly DbSet<NoteLinkScan> _scans = context.NoteLinkScans;

    // Both spellings, since older rows may carry the alias.
    private const string Markdown = "text/markdown";
    private const string MarkdownAlias = "text/x-markdown";

    private const string ReplaceSavepoint = "note_link_scan_replace";

    public async Task<NoteLinkScan?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _scans.FirstOrDefaultAsync(s => s.ContentObjectId == id, ct);
    }

    public async Task<NoteLinkScan?> FirstOrDefaultAsync(Expression<Func<NoteLinkScan, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _scans.FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IEnumerable<NoteLinkScan>> FindAsync(Expression<Func<NoteLinkScan, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _scans.Where(predicate).ToListAsync(ct);
    }

    public async Task<NoteLinkScan> AddAsync(NoteLinkScan entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;

        var entry = await _scans.AddAsync(entity, ct);

        return entry.Entity;
    }

    public async Task<IEnumerable<NoteLinkScan>> AddRangeAsync(IEnumerable<NoteLinkScan> entities,
        CancellationToken ct = default)
    {
        var scans = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var scan in scans)
            scan.CreatedAt = now;

        await _scans.AddRangeAsync(scans, ct);

        return scans;
    }

    public void Update(NoteLinkScan entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _scans.Update(entity);
    }

    public void Remove(NoteLinkScan entity)
    {
        _scans.Remove(entity);
    }

    public void RemoveRange(IEnumerable<NoteLinkScan> entities)
    {
        _scans.RemoveRange(entities);
    }

    public async Task<int> CountAsync(Expression<Func<NoteLinkScan, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        if (predicate is null) return await _scans.CountAsync(ct);

        return await _scans.CountAsync(predicate, ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<NoteLinkScan, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _scans.AnyAsync(predicate, ct);
    }

    public async Task<int?> GetScannerVersionAsync(Guid contentObjectId, CancellationToken ct = default)
    {
        return await _scans
            .Where(s => s.ContentObjectId == contentObjectId)
            .Select(s => (int?)s.ScannerVersion)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ReplaceAsync(NoteLinkScan scan, CancellationToken ct = default)
    {
        // The delete and the insert land together, so a failed insert keeps the links the note had.
        // Inside a caller's transaction a savepoint does the same without ending the caller's work.
        var callers = context.Database.CurrentTransaction;
        var owned = callers is null ? await context.Database.BeginTransactionAsync(ct) : null;

        if (callers is not null) await callers.CreateSavepointAsync(ReplaceSavepoint, ct);

        try
        {
            // The database cascade takes the old links with the old scan.
            await _scans
                .Where(s => s.ContentObjectId == scan.ContentObjectId)
                .ExecuteDeleteAsync(ct);

            scan.CreatedAt = DateTime.UtcNow;
            _scans.Add(scan);

            await context.SaveChangesAsync(ct);

            if (owned is not null) await owned.CommitAsync(ct);
            else await callers!.ReleaseSavepointAsync(ReplaceSavepoint, ct);

            return true;
        }
        catch (Exception ex)
        {
            // Rows left tracked would be written again by the next save on this context, which in
            // a checkpoint is the rest of the request, and fail it the same way.
            Detach(scan);

            if (owned is not null) await owned.RollbackAsync(CancellationToken.None);
            else await callers!.RollbackToSavepointAsync(ReplaceSavepoint, CancellationToken.None);

            if (ex is DbUpdateException update && IsUniqueViolation(update)) return false;

            throw;
        }
        finally
        {
            if (owned is not null) await owned.DisposeAsync();
        }
    }

    private void Detach(NoteLinkScan scan)
    {
        // Detaching a link takes it out of the scan's collection, so the loop runs over a copy.
        foreach (var link in scan.Links.ToList())
            context.Entry(link).State = EntityState.Detached;

        context.Entry(scan).State = EntityState.Detached;
    }

    public async Task<IReadOnlyList<NoteLinkScanCandidate>> GetScanCandidatesAsync(int scannerVersion, long maxBytes,
        int limit, CancellationToken ct = default)
    {
        var notesKind = (int)DirectorySystemKind.Notes;

        return await context.Database.SqlQuery<NoteLinkScanCandidate>($"""
            WITH RECURSIVE notes AS (
                SELECT "Id", "OwnerId"
                FROM "Directories"
                WHERE "SystemKind" = {notesKind}
                  AND "DeletedAt" IS NULL

                UNION

                SELECT d."Id", d."OwnerId"
                FROM "Directories" d
                JOIN notes n ON d."ParentId" = n."Id" AND d."OwnerId" = n."OwnerId"
                WHERE d."DeletedAt" IS NULL
            )
            SELECT v."ContentObjectId", v."Id" AS "VersionId", f."OwnerId"
            FROM "Files" f
            JOIN notes n ON n."Id" = f."DirectoryId" AND n."OwnerId" = f."OwnerId"
            JOIN "FileVersions" v ON v."Id" = f."CurrentVersionId"
            LEFT JOIN "NoteLinkScans" s ON s."ContentObjectId" = v."ContentObjectId"
            WHERE f."DeletedAt" IS NULL
              AND v."DeletedAt" IS NULL
              AND NOT v."IsEncrypted"
              AND v."MimeType" IN ({Markdown}, {MarkdownAlias})
              AND v."Size" <= {maxBytes}
              AND (s."ContentObjectId" IS NULL OR s."ScannerVersion" < {scannerVersion})
            ORDER BY v."CreatedAt" DESC
            LIMIT {limit}
            """)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<NoteBacklinkDto>> GetBacklinksAsync(LinkTargetKind kind, Guid targetId,
        Guid userId, int limit, CancellationToken ct = default)
    {
        var notesKind = (int)DirectorySystemKind.Notes;
        var targetKind = (int)kind;

        // A file's current version is one row, and a scan has one link per target, so every note
        // appears once.
        return await context.Database.SqlQuery<NoteBacklinkDto>($"""
            WITH RECURSIVE notes AS (
                SELECT "Id"
                FROM "Directories"
                WHERE "OwnerId" = {userId}
                  AND "SystemKind" = {notesKind}
                  AND "DeletedAt" IS NULL

                UNION

                SELECT d."Id"
                FROM "Directories" d
                JOIN notes n ON d."ParentId" = n."Id"
                WHERE d."OwnerId" = {userId}
                  AND d."DeletedAt" IS NULL
            )
            SELECT f."Id" AS "FileId",
                   f."Name",
                   f."DirectoryId",
                   COALESCE(f."UpdatedAt", f."CreatedAt") AS "UpdatedAt",
                   l."Snippet"
            FROM "NoteLinks" l
            JOIN "FileVersions" v ON v."ContentObjectId" = l."ContentObjectId"
            JOIN "Files" f ON f."CurrentVersionId" = v."Id"
            JOIN notes n ON n."Id" = f."DirectoryId"
            WHERE l."TargetKind" = {targetKind}
              AND l."TargetId" = {targetId}
              AND f."OwnerId" = {userId}
              AND f."DeletedAt" IS NULL
              AND f."Id" <> {targetId}
              AND v."DeletedAt" IS NULL
              AND NOT v."IsEncrypted"
              AND v."MimeType" IN ({Markdown}, {MarkdownAlias})
            ORDER BY COALESCE(f."UpdatedAt", f."CreatedAt") DESC
            LIMIT {limit}
            """)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BacklinkCount>> CountBacklinksAsync(LinkTargetKind kind,
        IReadOnlyCollection<Guid> targetIds, Guid userId, CancellationToken ct = default)
    {
        var notesKind = (int)DirectorySystemKind.Notes;
        var targetKind = (int)kind;
        var ids = targetIds.ToArray();

        // The same notes the backlink list shows, a note linking to itself left out.
        return await context.Database.SqlQuery<BacklinkCount>($"""
            WITH RECURSIVE notes AS (
                SELECT "Id"
                FROM "Directories"
                WHERE "OwnerId" = {userId}
                  AND "SystemKind" = {notesKind}
                  AND "DeletedAt" IS NULL

                UNION

                SELECT d."Id"
                FROM "Directories" d
                JOIN notes n ON d."ParentId" = n."Id"
                WHERE d."OwnerId" = {userId}
                  AND d."DeletedAt" IS NULL
            )
            SELECT l."TargetId", COUNT(*)::int AS "Count"
            FROM "NoteLinks" l
            JOIN "FileVersions" v ON v."ContentObjectId" = l."ContentObjectId"
            JOIN "Files" f ON f."CurrentVersionId" = v."Id"
            JOIN notes n ON n."Id" = f."DirectoryId"
            WHERE l."TargetKind" = {targetKind}
              AND l."TargetId" = ANY({ids})
              AND f."OwnerId" = {userId}
              AND f."DeletedAt" IS NULL
              AND f."Id" <> l."TargetId"
              AND v."DeletedAt" IS NULL
              AND NOT v."IsEncrypted"
              AND v."MimeType" IN ({Markdown}, {MarkdownAlias})
            GROUP BY l."TargetId"
            """)
            .ToListAsync(ct);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
