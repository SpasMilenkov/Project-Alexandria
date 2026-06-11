using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class SignedUrlRepository(AlexandriaDbContext context) : ISignedUrlRepository
{
    private readonly DbSet<SignedUrl> _signedUrls = context.Set<SignedUrl>();

    public async Task<SignedUrl?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return await _signedUrls
            .Include(s => s.FileInfo)
            .Where(s => s.Token == token && s.DeletedAt == null)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<SignedUrl>> GetByFileIdAsync(Guid fileId, CancellationToken ct = default)
    {
        return await _signedUrls
            .Where(s => s.FileId == fileId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<SignedUrl> CreateAsync(SignedUrl entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.CreatedAt = DateTime.UtcNow;

        var entry = await _signedUrls.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return entry.Entity;
    }

    public async Task<bool> RevokeAsync(Guid id, string userId, CancellationToken ct = default)
    {
        var entity = await _signedUrls
            .Where(s => s.Id == id && s.CreatorId == userId && s.DeletedAt == null)
            .FirstOrDefaultAsync(ct);

        if (entity is null)
            return false;

        entity.DeletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SignedUrl?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _signedUrls.FindAsync([id], ct);

    public async Task<SignedUrl?> FirstOrDefaultAsync(
        Expression<Func<SignedUrl, bool>> predicate,
        CancellationToken ct = default)
        => await _signedUrls.FirstOrDefaultAsync(predicate, ct);

    public async Task<IEnumerable<SignedUrl>> FindAsync(
        Expression<Func<SignedUrl, bool>> predicate,
        CancellationToken ct = default)
        => await _signedUrls.Where(predicate).ToListAsync(ct);

    public async Task<SignedUrl> AddAsync(SignedUrl entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.CreatedAt = DateTime.UtcNow;
        var entry = await _signedUrls.AddAsync(entity, ct);
        return entry.Entity;
    }

    public Task<IEnumerable<SignedUrl>> AddRangeAsync(IEnumerable<SignedUrl> entities, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public void Update(SignedUrl entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.UpdatedAt = DateTime.UtcNow;
        _signedUrls.Update(entity);
    }

    public void Remove(SignedUrl entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.DeletedAt = DateTime.UtcNow;
        _signedUrls.Update(entity);
    }

    public void RemoveRange(IEnumerable<SignedUrl> entities)
    {
        throw new NotImplementedException();
    }

    public async Task<int> CountAsync(
        Expression<Func<SignedUrl, bool>>? predicate = null,
        CancellationToken ct = default)
        => predicate is null
            ? await _signedUrls.CountAsync(ct)
            : await _signedUrls.CountAsync(predicate, ct);

    public async Task<bool> ExistsAsync(
        Expression<Func<SignedUrl, bool>> predicate,
        CancellationToken ct = default)
        => await _signedUrls.AnyAsync(predicate, ct);

    public async Task IncrementAccessCountAsync(Guid id, CancellationToken ct = default)
    {
        await _signedUrls
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.AccessCount, x => x.AccessCount + 1)
                .SetProperty(x => x.LastAccessedAt, _ => DateTime.UtcNow), ct);
    }
}