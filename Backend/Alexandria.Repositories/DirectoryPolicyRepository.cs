using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Dto.Policies;
using Alexandria.Repositories.Projections;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public sealed class DirectoryPolicyRepository(AlexandriaDbContext context)
    : IDirectoryPolicyRepository
{
    private readonly DbSet<DirectoryPolicy> _policies = context.DirectoryPolicies;

    public async Task<DirectoryPolicy> CreateAsync(DirectoryPolicy policy, CancellationToken ct = default)
    {
        policy.CreatedAt = DateTime.UtcNow;
        policy.UpdatedAt = DateTime.UtcNow;
        await _policies.AddAsync(policy, ct);
        await context.SaveChangesAsync(ct);
        return policy;
    }

    public async Task<DirectoryPolicy> UpdateAsync(DirectoryPolicy policy, CancellationToken ct = default)
    {
        policy.UpdatedAt = DateTime.UtcNow;
        _policies.Update(policy);
        await context.SaveChangesAsync(ct);
        return policy;
    }

    public void Add(DirectoryPolicy entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _policies.Add(entity);
    }

    public async Task<DirectoryPolicy> AddAsync(DirectoryPolicy entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await _policies.AddAsync(entity, ct);
        return entity;
    }

    public async Task<IEnumerable<DirectoryPolicy>> AddRangeAsync(IEnumerable<DirectoryPolicy> entities,
        CancellationToken ct = default)
    {
        var list = entities.ToList();
        var now = DateTime.UtcNow;
        foreach (var e in list)
        {
            e.CreatedAt = now;
            e.UpdatedAt = now;
        }

        await _policies.AddRangeAsync(list, ct);
        return list;
    }

    public void Update(DirectoryPolicy entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _policies.Update(entity);
    }

    public void Remove(DirectoryPolicy entity)
    {
        entity.DeletedAt = DateTime.UtcNow;
        _policies.Update(entity);
    }

    public void RemoveRange(IEnumerable<DirectoryPolicy> entities)
    {
        var now = DateTime.UtcNow;
        foreach (var e in entities)
            e.DeletedAt = now;
        _policies.UpdateRange(entities);
    }

    public async Task<DirectoryPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _policies
            .Where(p => p.Id == id && p.DeletedAt == null)
            .Include(p => p.Rules.Where(r => r.DeletedAt == null))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<DirectoryPolicy?> FirstOrDefaultAsync(
        Expression<Func<DirectoryPolicy, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _policies
            .Where(p => p.DeletedAt == null)
            .Where(predicate)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<DirectoryPolicy>> FindAsync(
        Expression<Func<DirectoryPolicy, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _policies
            .Where(p => p.DeletedAt == null)
            .Where(predicate)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(
        Expression<Func<DirectoryPolicy, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        var query = _policies.Where(p => p.DeletedAt == null);
        return predicate is null
            ? await query.CountAsync(ct)
            : await query.CountAsync(predicate, ct);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<DirectoryPolicy, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _policies
            .Where(p => p.DeletedAt == null)
            .AnyAsync(predicate, ct);
    }

    public async Task<DirectoryPolicyDto?> GetByDirectoryIdAsync(Guid directoryId, CancellationToken ct = default)
    {
        var entity = await _policies
            .Where(p => p.DirectoryId == directoryId && p.DeletedAt == null)
            .Include(p => p.Rules.Where(r => r.DeletedAt == null))
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : DirectoryPolicyDto.FromEntity(entity);
    }

    public async Task<DirectoryPolicyDto?> ResolveEffectivePolicyAsync(Guid directoryId, CancellationToken ct = default)
    {
        var direct = await GetByDirectoryIdAsync(directoryId, ct);
        if (direct is not null) return direct;

        // The starting folder is excluded: its own policy was checked above, and an ancestor
        // only counts when it shares its rules with nested folders.
        var ancestors = await context.Database.SqlQuery<DirectoryAncestorRow>($"""
            WITH RECURSIVE chain AS (
                SELECT "Id", "ParentId", 0 AS "Depth"
                FROM "Directories"
                WHERE "Id" = {directoryId}
                  AND "DeletedAt" IS NULL

                UNION ALL

                SELECT d."Id", d."ParentId", c."Depth" + 1
                FROM "Directories" d
                JOIN chain c ON d."Id" = c."ParentId"
                WHERE d."DeletedAt" IS NULL
            )
            SELECT "Id", "Depth"
            FROM chain
            WHERE "Depth" > 0
            """)
            .ToListAsync(ct);

        if (ancestors.Count == 0) return null;

        var ancestorIds = ancestors.Select(a => a.Id).ToList();

        var inheritable = await _policies
            .Where(p => ancestorIds.Contains(p.DirectoryId) && p.InheritedByChildren && p.DeletedAt == null)
            .Include(p => p.Rules.Where(r => r.DeletedAt == null))
            .ToListAsync(ct);

        return PickNearest(ancestors, inheritable);
    }

    internal static DirectoryPolicyDto? PickNearest(
        IReadOnlyCollection<DirectoryAncestorRow> ancestors,
        IReadOnlyCollection<DirectoryPolicy> inheritable)
    {
        if (inheritable.Count == 0) return null;

        var depthById = ancestors.ToDictionary(a => a.Id, a => a.Depth);

        var nearest = inheritable
            .Where(p => depthById.ContainsKey(p.DirectoryId))
            .MinBy(p => depthById[p.DirectoryId]);

        return nearest is null ? null : DirectoryPolicyDto.FromEntity(nearest);
    }

    public async Task<bool> ExistsForDirectoryAsync(Guid directoryId, CancellationToken ct = default)
    {
        return await _policies
            .AnyAsync(p => p.DirectoryId == directoryId && p.DeletedAt == null, ct);
    }
}