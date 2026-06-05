using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Dto.Policies;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public sealed class PolicyRuleRepository(AlexandriaDbContext context)
    : IPolicyRuleRepository
{
    private readonly DbSet<PolicyRule> _rules = context.PolicyRules;

    public async Task<PolicyRule> CreateAsync(PolicyRule rule, CancellationToken ct = default)
    {
        rule.CreatedAt = DateTime.UtcNow;
        rule.UpdatedAt = DateTime.UtcNow;
        await _rules.AddAsync(rule, ct);
        await context.SaveChangesAsync(ct);
        return rule;
    }

    public async Task<PolicyRule> UpdateAsync(PolicyRule rule, CancellationToken ct = default)
    {
        rule.UpdatedAt = DateTime.UtcNow;
        _rules.Update(rule);
        await context.SaveChangesAsync(ct);
        return rule;
    }

    public void Add(PolicyRule entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _rules.Add(entity);
    }

    public async Task<PolicyRule> AddAsync(PolicyRule entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await _rules.AddAsync(entity, ct);
        return entity;
    }

    public async Task<IEnumerable<PolicyRule>> AddRangeAsync(IEnumerable<PolicyRule> entities,
        CancellationToken ct = default)
    {
        var list = entities.ToList();
        var now = DateTime.UtcNow;
        foreach (var e in list)
        {
            e.CreatedAt = now;
            e.UpdatedAt = now;
        }

        await _rules.AddRangeAsync(list, ct);
        return list;
    }

    public void Update(PolicyRule entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _rules.Update(entity);
    }

    public void Remove(PolicyRule entity)
    {
        entity.DeletedAt = DateTime.UtcNow;
        _rules.Update(entity);
    }

    public void RemoveRange(IEnumerable<PolicyRule> entities)
    {
        var now = DateTime.UtcNow;
        foreach (var e in entities)
            e.DeletedAt = now;
        _rules.UpdateRange(entities);
    }

    public async Task<PolicyRule?> GetByIdAsync(Guid ruleId, CancellationToken ct = default)
    {
        return await _rules
            .Where(r => r.Id == ruleId && r.DeletedAt == null)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PolicyRule?> FirstOrDefaultAsync(
        Expression<Func<PolicyRule, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _rules
            .Where(r => r.DeletedAt == null)
            .Where(predicate)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<PolicyRule>> FindAsync(
        Expression<Func<PolicyRule, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _rules
            .Where(r => r.DeletedAt == null)
            .Where(predicate)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(
        Expression<Func<PolicyRule, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        var query = _rules.Where(r => r.DeletedAt == null);
        return predicate is null
            ? await query.CountAsync(ct)
            : await query.CountAsync(predicate, ct);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<PolicyRule, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _rules
            .Where(r => r.DeletedAt == null)
            .AnyAsync(predicate, ct);
    }

    public async Task<IEnumerable<PolicyRuleDto>> GetByPolicyIdAsync(Guid policyId, CancellationToken ct = default)
    {
        var entities = await _rules
            .Where(r => r.PolicyId == policyId && r.DeletedAt == null)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

        return entities.Select(PolicyRuleDto.FromEntity);
    }
}