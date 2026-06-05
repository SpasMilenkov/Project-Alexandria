using Alexandria.Dto.Policies;

namespace Alexandria.Common.Services;

public interface IDirectoryPolicyService
{
    /// <summary>Returns the policy for a directory, or null if none exists.</summary>
    Task<DirectoryPolicyDto?> GetPolicyAsync(Guid directoryId, Guid ownerId, CancellationToken ct = default);

    /// <summary>Creates a new policy for a directory. Throws if one already exists.</summary>
    Task<DirectoryPolicyDto> CreatePolicyAsync(CreateDirectoryPolicyRequest request, Guid ownerId,
        CancellationToken ct = default);

    /// <summary>Updates the top-level policy settings (inheritance flag). Does not touch rules.</summary>
    Task<DirectoryPolicyDto> UpdatePolicyAsync(Guid policyId, bool inheritedByChildren, Guid updatedBy,
        CancellationToken ct = default);

    /// <summary>Soft-deletes the policy and all its rules.</summary>
    Task DeletePolicyAsync(Guid policyId, Guid requestedBy, CancellationToken ct = default);

    /// <summary>Adds a rule to an existing policy.</summary>
    Task<PolicyRuleDto> AddRuleAsync(Guid policyId, CreatePolicyRuleRequest request, Guid updatedBy,
        CancellationToken ct = default);

    /// <summary>Updates an existing rule's trigger, priority, and parameters.</summary>
    Task<PolicyRuleDto> UpdateRuleAsync(Guid ruleId, UpdatePolicyRuleRequest request, Guid updatedBy,
        CancellationToken ct = default);

    /// <summary>Soft-deletes a single rule.</summary>
    Task DeleteRuleAsync(Guid ruleId, Guid requestedBy, CancellationToken ct = default);
}