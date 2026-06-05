using Alexandria.Data.Models;
using Alexandria.Dto.Policies;

namespace Alexandria.Common.Repositories;

/// <summary>Repository for <see cref="PolicyRule"/> persistence operations.</summary>
public interface IPolicyRuleRepository : IRepository<PolicyRule>
{
    /// <summary>Creates and persists a new rule, returning the saved instance.</summary>
    Task<PolicyRule> CreateAsync(PolicyRule rule, CancellationToken ct = default);

    /// <summary>Persists changes to an existing rule and returns the updated instance.</summary>
    Task<PolicyRule> UpdateAsync(PolicyRule rule, CancellationToken ct = default);

    /// <summary>Returns all active rules for a given policy as DTOs ordered by priority ascending.</summary>
    Task<IEnumerable<PolicyRuleDto>> GetByPolicyIdAsync(Guid policyId, CancellationToken ct = default);

    /// <summary>Returns a single rule entity by id, or null if not found or soft-deleted.</summary>
    Task<PolicyRule?> GetByIdAsync(Guid ruleId, CancellationToken ct = default);
}