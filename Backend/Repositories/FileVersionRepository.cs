using System.Linq.Expressions;
using Common.Repositories;
using Data.Context;
using DTO.Files;
using Microsoft.EntityFrameworkCore;
using Models;

namespace Repositories;

public class FileVersionRepository : IFileVersionRepository
{
    private readonly DbSet<FileVersion> _dbSet;
    private readonly AlexandriaDbContext _context;

    public FileVersionRepository(AlexandriaDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = _context.Set<FileVersion>();
    }

    public async Task<FileVersion?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(fv => fv.ContentObject)
            .Include(fv => fv.File)
            .FirstOrDefaultAsync(fv => fv.Id == id, ct);
    }


    public async Task<FileVersion?> FirstOrDefaultAsync(
        Expression<Func<FileVersion, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet
            .Include(fv => fv.ContentObject)
            .Include(fv => fv.File)
            .FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IEnumerable<FileVersion>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbSet
            .Include(fv => fv.ContentObject)
            .Include(fv => fv.File)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<FileVersion>> FindAsync(
        Expression<Func<FileVersion, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet
            .Include(fv => fv.ContentObject)
            .Include(fv => fv.File)
            .Where(predicate)
            .ToListAsync(ct);
    }

    public async Task<FileVersion> AddAsync(FileVersion entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.CreatedAt = DateTime.UtcNow;

        var entry = await _dbSet.AddAsync(entity, ct);
        return entry.Entity;
    }

    public async Task<IEnumerable<FileVersion>> AddRangeAsync(
        IEnumerable<FileVersion> entities,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var fileVersions = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var entity in fileVersions)
        {
            entity.CreatedAt = now;
        }

        await _dbSet.AddRangeAsync(fileVersions, ct);
        return fileVersions;
    }

    public void Update(FileVersion entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt = DateTime.UtcNow;
        _dbSet.Update(entity);
    }

    public void Remove(FileVersion entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.DeletedAt = DateTime.UtcNow;
        _dbSet.Update(entity);
    }

    public void RemoveRange(IEnumerable<FileVersion> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var now = DateTime.UtcNow;
        var fileVersions = entities.ToList();

        foreach (var entity in fileVersions)
        {
            entity.DeletedAt = now;
        }

        _dbSet.UpdateRange(fileVersions);
    }

    public async Task<int> CountAsync(
        Expression<Func<FileVersion, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        return predicate == null
            ? await _dbSet.CountAsync(ct)
            : await _dbSet.CountAsync(predicate, ct);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<FileVersion, bool>> predicate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return await _dbSet.AnyAsync(predicate, ct);
    }

    public async Task<int> DeleteFileVersions(Guid[] fileIds, Guid ownerId, CancellationToken ct = default)
    {
        return await _context.FileVersions
            .Where(v => fileIds.Contains(v.FileId) && v.File.OwnerId == ownerId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(v => v.DeletedAt, _ => DateTime.UtcNow),
                ct);
    }

    public async Task<int> RestoreFileVersions(
        Guid[] fileIds,
        Guid ownerId,
        CancellationToken ct = default)
    {
        var thresholdDate = DateTime.UtcNow.AddDays(-30);

        var versions = await _context.FileVersions
            .Where(v =>
                fileIds.Contains(v.FileId) &&
                v.File.OwnerId == ownerId &&
                v.DeletedAt != null &&
                v.DeletedAt > thresholdDate)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        foreach (var version in versions)
        {
            version.DeletedAt = null;
            version.UpdatedAt = now;
            version.UpdatedBy = ownerId;
        }

        return versions.Count;
    }

    public async Task<PaginatedResult<FileVersionDto>> GetVersionsForFile(Guid fileId, Guid ownerId, int page = 1,
        int pageSize = 10, CancellationToken ct = default)
    {
        var baseQuery = _dbSet.Where(f => f.FileId == fileId && f.File.OwnerId == ownerId &&
                                          (f.DeletedAt == null || f.DeletedAt >= DateTime.UtcNow.AddDays(-30)));

        var totalCount = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f =>
                new FileVersionDto(f.Id, f.Size, f.MimeType, f.VersionNumber, f.CreatedAt, f.DeletedAt != null, f.IsEncrypted))
            .ToListAsync(ct);

        return new PaginatedResult<FileVersionDto>
        {
            CurrentPage = page,
            Items = items,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }


    public async Task<int> SoftDeleteFileVersions(Guid[] fileIds, Guid ownerId, CancellationToken ct = default)
    {
        return await _context.FileVersions
            .Where(v => fileIds.Contains(v.FileId) && v.File.OwnerId == ownerId && v.DeletedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(v => v.DeletedAt, _ => DateTime.UtcNow),
                ct);
    }

    public async Task<int> SoftDeleteFileVersions(Guid fileId, Guid ownerId, CancellationToken ct = default)
    {
        return await _context.FileVersions
            .Where(v => v.FileId == fileId && v.File.OwnerId == ownerId && v.DeletedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(v => v.DeletedAt, _ => DateTime.UtcNow),
                ct);
    }

    public async Task<byte[]?> GetContentHashByVersionId(Guid versionId, Guid userId, CancellationToken ct = default)
    {
        return await _dbSet.Where(f => f.Id == versionId && f.File.OwnerId == userId).Select(f => f.ContentHash)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<FileVersionDto?> GetMostRecent(Guid fileId, Guid userId, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(f => f.FileId == fileId && f.File.OwnerId == userId && f.DeletedAt == null)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FileVersionDto(f.Id, f.Size, f.MimeType, f.VersionNumber, f.CreatedAt, f.DeletedAt == null, f.IsEncrypted))
            .FirstOrDefaultAsync(ct);
    }

    public async Task RestoreFileVersion(Guid versionId, Guid userId, CancellationToken ct = default)
    {
        // Restore the version
        var updated = await _dbSet
            .Where(v => v.Id == versionId && v.File.OwnerId == userId && v.DeletedAt != null)
            .ExecuteUpdateAsync(v => v
                .SetProperty(v => v.UpdatedAt, DateTime.UtcNow)
                .SetProperty(v => v.DeletedAt, (DateTime?)null), ct);

        if (updated == 0)
            throw new InvalidOperationException("Version not found or not eligible for restore");

        // Get the file ID and its current state
        var version = await _dbSet
            .Where(v => v.Id == versionId)
            .Select(v => new { v.FileId, FileDeletedAt = v.File.DeletedAt })
            .FirstAsync(ct);

        // Always set this version as active
        await _context.Files
            .Where(f => f.Id == version.FileId)
            .ExecuteUpdateAsync(f => f
                .SetProperty(f => f.CurrentVersionId, versionId)
                .SetProperty(f => f.UpdatedAt, DateTime.UtcNow), ct);

        // If the file itself was soft-deleted, restore it too
        if (version.FileDeletedAt != null)
        {
            await _context.Files
                .Where(f => f.Id == version.FileId)
                .ExecuteUpdateAsync(f => f
                    .SetProperty(f => f.DeletedAt, (DateTime?)null), ct);
        }

    }

    public async Task RemoveAsync(Guid versionId, Guid ownerId, CancellationToken ct = default)
    {
        await _dbSet
            .Where(v => v.Id == versionId && v.File.OwnerId == ownerId)
            .ExecuteUpdateAsync(v => v
                .SetProperty(v => v.DeletedAt, DateTime.UtcNow)
                .SetProperty(v => v.UpdatedAt, DateTime.UtcNow), ct);
    }

    public async Task<VersionDownloadInfo?> GetVersionDownloadInfo(Guid versionId, Guid userId, CancellationToken ct = default)
    {
        return await _dbSet.Where(v => v.Id == versionId && v.File.OwnerId == userId).Select(v => new VersionDownloadInfo(v.File.Name, v.File.MimeType, v.VersionNumber, v.ContentHash)).FirstOrDefaultAsync(ct);
    }

    public async Task<DownloadMetadata?> GetDownloadMetadataAsync(Guid versionId, Guid userId, CancellationToken ct = default)
    {
        return await _dbSet.Where(v => v.Id == versionId && v.File.OwnerId == userId && v.DeletedAt == null).Select(v => new DownloadMetadata
        {
            FileName = v.File.Name,
            MimeType = v.File.MimeType,
            EncryptionHint = v.EncryptionHint,
            EncryptionIv = v.EncryptionIv,
            IsEncrypted = v.IsEncrypted,
            EncryptionSalt = v.EncryptionSalt,
            IntegrityTag = v.IntegrityTag,
        }).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> IsEncrypted(Guid versionId, CancellationToken ct = default)
    {
        return await _dbSet.Where(v => v.Id == versionId).Select(v => v.IsEncrypted).FirstAsync(ct);
    }

    public async Task<bool> IsPromoted(Guid versionId, CancellationToken ct = default)
    {
        return await _dbSet.Where(v => v.Id == versionId).Select(v => v.ContentObject.IsPromoted).FirstAsync(ct);
    }
}
