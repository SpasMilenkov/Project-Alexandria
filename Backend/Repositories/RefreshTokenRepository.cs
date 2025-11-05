using System.Linq.Expressions;
using Common;
using Common.Repositories;
using Data.Context;
using Microsoft.EntityFrameworkCore;
using Models;

namespace Repositories;

public class RefreshTokenRepository(AlexandriaDbContext context) : IRefreshTokenRepository
{
    private readonly DbSet<RefreshToken> _refreshTokens = context.RefreshTokens;

    public async Task<RefreshToken?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _refreshTokens
            .Where(rt => rt.DeletedAt == null)
            .FirstOrDefaultAsync(rt => rt.Id == id, ct);
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return await _refreshTokens
            .Where(rt => rt.DeletedAt == null)
            .FirstOrDefaultAsync(rt => rt.Token == token, ct);
    }

    public async Task<RefreshToken?> GetByTokenWithUserAsync(string token, CancellationToken ct = default)
    {
        return await _refreshTokens
            .Include(rt => rt.User)
            .Where(rt => rt.DeletedAt == null)
            .FirstOrDefaultAsync(rt => rt.Token == token && !rt.IsRevoked, ct);
    }

    public async Task<RefreshToken?> FirstOrDefaultAsync(Expression<Func<RefreshToken, bool>> predicate, CancellationToken ct = default)
    {
        return await _refreshTokens
            .Where(rt => rt.DeletedAt == null)
            .FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IEnumerable<RefreshToken>> GetAllAsync(CancellationToken ct = default)
    {
        return await _refreshTokens
            .Where(rt => rt.DeletedAt == null)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<RefreshToken>> FindAsync(Expression<Func<RefreshToken, bool>> predicate, CancellationToken ct = default)
    {
        return await _refreshTokens
            .Where(rt => rt.DeletedAt == null)
            .Where(predicate)
            .ToListAsync(ct);
    }

    public async Task<RefreshToken> AddAsync(RefreshToken entity, CancellationToken ct = default)
    {
        // Set audit fields
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = null;
        entity.DeletedAt = null;

        var result = await _refreshTokens.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<RefreshToken> UpdateAsync(RefreshToken entity, CancellationToken ct = default)
    {
        var existingToken = await GetByIdAsync(entity.Id, ct);
        if (existingToken == null)
        {
            throw new InvalidOperationException($"RefreshToken with ID {entity.Id} not found or has been deleted.");
        }

        // Update mutable properties
        existingToken.IsRevoked = entity.IsRevoked;
        existingToken.RevokedAt = entity.RevokedAt;
        existingToken.UpdatedBy = entity.UpdatedBy;
        existingToken.UpdatedAt = DateTime.UtcNow;

        _refreshTokens.Update(existingToken);
        await context.SaveChangesAsync(ct);

        return existingToken;
    }

    public void Update(RefreshToken entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _refreshTokens.Update(entity);
    }

    public void Remove(RefreshToken entity)
    {
        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _refreshTokens.Update(entity);
    }

    public void RemoveRange(IEnumerable<RefreshToken> entities)
    {
        var now = DateTime.UtcNow;
        var refreshTokens = entities as RefreshToken[] ?? entities.ToArray();
        foreach (var entity in refreshTokens)
        {
            entity.DeletedAt = now;
            entity.UpdatedAt = now;
        }

        _refreshTokens.UpdateRange(refreshTokens);
    }

    public async Task<int> CountAsync(Expression<Func<RefreshToken, bool>>? predicate = null, CancellationToken ct = default)
    {
        var query = _refreshTokens.Where(rt => rt.DeletedAt == null);

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        return await query.CountAsync(ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<RefreshToken, bool>> predicate, CancellationToken ct = default)
    {
        return await _refreshTokens
            .Where(rt => rt.DeletedAt == null)
            .AnyAsync(predicate, ct);
    }

    public async Task<bool> RevokeTokenAsync(string token, CancellationToken ct = default)
    {
        var storedToken = await _refreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token && rt.DeletedAt == null, ct);

        if (storedToken == null)
        {
            return false;
        }

        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.UpdatedAt = DateTime.UtcNow;

        _refreshTokens.Update(storedToken);
        await context.SaveChangesAsync(ct);

        return true;
    }

    public async Task RevokeUserTokensAsync(Guid userId, CancellationToken ct = default)
    {
        var userTokens = await _refreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked && rt.DeletedAt == null)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var token in userTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = now;
            token.UpdatedAt = now;
        }

        _refreshTokens.UpdateRange(userTokens);
        await context.SaveChangesAsync(ct);
    }
}