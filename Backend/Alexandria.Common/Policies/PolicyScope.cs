using Alexandria.Data.Models;
using Directory = Alexandria.Data.Models.Directory;

namespace Alexandria.Common.Policies;

/// <summary>Which folders a policy governs, under the same nearest-policy-wins rule uploads use.</summary>
public static class PolicyScope
{
    /// <summary>
    /// The folders a sweep of this policy's rules covers, so a backfill reaches the same files an
    /// upload under this policy would.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> ResolveGovernedDirectoryIdsAsync(
        IUnitOfWork unitOfWork, DirectoryPolicy policy, CancellationToken ct = default)
    {
        if (!policy.InheritedByChildren) return [policy.DirectoryId];

        var subdirectories = (await unitOfWork.Directories.GetAllSubDirectoriesAsync(policy.DirectoryId, ct)).ToList();
        var subdirectoryIds = subdirectories.Select(d => d.Id).ToList();

        var subdirectoryPolicies = await unitOfWork.DirectoryPolicies
            .GetWithRulesByDirectoryIdsAsync(subdirectoryIds, ct);

        return GovernedDirectoryIds(policy.DirectoryId, subdirectories, subdirectoryPolicies);
    }

    /// <summary>
    /// For a policy its children inherit: its own folder plus every descendant whose effective
    /// policy is this one. A descendant with its own policy is governed by that policy instead,
    /// and so is its whole subtree when that policy is inherited too. A descendant policy without
    /// live rules governs nothing, so it leaves its folder with us, as uploads do.
    /// </summary>
    /// <param name="subdirectoryPolicies">Descendant policies with their live rules loaded.</param>
    public static IReadOnlyList<Guid> GovernedDirectoryIds(
        Guid rootDirectoryId,
        IEnumerable<Directory> subdirectories,
        IEnumerable<DirectoryPolicy> subdirectoryPolicies)
    {
        var childrenByParent = subdirectories.ToLookup(d => d.ParentId);

        var ownPolicyIsInherited = subdirectoryPolicies
            .Where(p => p.DeletedAt == null && p.Rules.Any(r => r.DeletedAt == null))
            .GroupBy(p => p.DirectoryId)
            .ToDictionary(g => g.Key, g => g.Any(p => p.InheritedByChildren));

        var governed = new List<Guid> { rootDirectoryId };
        var pending = new Stack<Guid>(childrenByParent[rootDirectoryId].Select(d => d.Id));

        while (pending.Count > 0)
        {
            var directoryId = pending.Pop();

            if (ownPolicyIsInherited.TryGetValue(directoryId, out var inherited))
            {
                if (inherited) continue;

                // A policy only for this folder leaves its children to the nearest inherited one,
                // which is ours.
            }
            else
            {
                governed.Add(directoryId);
            }

            foreach (var child in childrenByParent[directoryId])
                pending.Push(child.Id);
        }

        return governed;
    }
}
