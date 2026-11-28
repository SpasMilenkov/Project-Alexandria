using Alexandria.Data.Models;
using Alexandria.Dto.Policies;

namespace Alexandria.Common.Repositories;

/// <summary>Repository for <see cref="DirectoryPolicy"/> persistence operations.</summary>
public interface IDirectoryPolicyRepository : IRepository<DirectoryPolicy>
{
    /// <summary>Creates and persists a new policy, returning the saved instance.</summary>
    Task<DirectoryPolicy> CreateAsync(DirectoryPolicy policy, CancellationToken ct = default);

    /// <summary>Persists changes to an existing policy and returns the updated instance.</summary>
    Task<DirectoryPolicy> UpdateAsync(DirectoryPolicy policy, CancellationToken ct = default);

    /// <summary>
    /// Returns the policy DTO for the given directory including its rules, or null if none exists.
    /// Excludes soft-deleted policies and rules.
    /// </summary>
    Task<DirectoryPolicyDto?> GetByDirectoryIdAsync(Guid directoryId, CancellationToken ct = default);

    /// <summary>
    /// Resolves the effective policy for a directory, checking the directory first
    /// then walking up to the nearest ancestor with an inheritable policy. A policy without
    /// live rules is passed over, so an emptied folder falls back to its parent's rules.
    /// Returns null if no applicable policy is found.
    /// </summary>
    Task<DirectoryPolicyDto?> ResolveEffectivePolicyAsync(Guid directoryId, CancellationToken ct = default);

    /// <summary>
    /// The nearest ancestor policy that is inherited and has live rules, ignoring the directory's
    /// own policy. It is what the directory, or its subfolders, fall back to when the directory's
    /// own policy stops applying. Returns null if there is none.
    /// </summary>
    Task<DirectoryPolicyDto?> ResolveInheritedPolicyAboveAsync(Guid directoryId, CancellationToken ct = default);

    /// <summary>
    /// Live policies of the given directories with their live rules loaded. No tracking, unbounded.
    /// </summary>
    Task<IReadOnlyList<DirectoryPolicy>> GetWithRulesByDirectoryIdsAsync(
        IReadOnlyCollection<Guid> directoryIds, CancellationToken ct = default);

    /// <summary>Returns whether a policy exists for the given directory.</summary>
    Task<bool> ExistsForDirectoryAsync(Guid directoryId, CancellationToken ct = default);
}