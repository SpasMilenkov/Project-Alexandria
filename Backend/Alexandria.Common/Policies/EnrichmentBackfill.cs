using System.Linq.Expressions;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Common.Policies;

/// <summary>
/// Policy backfill sweep primitives. A directory policy only ever fires for
/// files finalized after its creation; this fills the gap for files that already
/// existed under the policy's directory by reusing the normal enrich trigger path per file, so
/// completion flows back through the same results-consumer → auto-tag queue path as a
/// fresh upload. No playlist-aware completion path exists or is needed.
/// </summary>
public static class EnrichmentBackfill
{
    /// <summary>
    /// Routing key for backfill requests. The body is a raw policy id (same Guid-bytes
    /// convention as the enrich trigger). Bound to its own queue so the trigger
    /// consumer never mistakes a policy id for a file id.
    /// </summary>
    public const string BackfillRoutingKey = "media-metadata.enrich-backfill";

    /// <summary>
    /// Files in scope that predate the policy: live, sitting directly in one of
    /// <paramref name="directoryIds"/>, created strictly before
    /// <paramref name="predatingCutoff"/> (the policy's own <c>CreatedAt</c>).
    /// Single-sourced: the repository executes this, unit tests compile it.
    /// </summary>
    public static Expression<Func<File, bool>> BuildScopeFilter(
        IReadOnlyCollection<Guid> directoryIds,
        DateTime predatingCutoff)
    {
        return f => f.DeletedAt == null
                    && f.CreatedAt < predatingCutoff
                    && f.DirectoryId.HasValue
                    && directoryIds.Contains(f.DirectoryId.Value);
    }

    /// <summary>
    /// Drops candidates that already have a successful enrichment job, preserving
    /// candidate order. Mirrors the auto-tag backstop's in-memory set difference.
    /// </summary>
    public static IReadOnlyList<Guid> RemoveAlreadyEnriched(
        IEnumerable<Guid> candidates,
        IEnumerable<Guid> readyFileIds)
    {
        var ready = readyFileIds.ToHashSet();
        return candidates.Where(id => !ready.Contains(id)).ToList();
    }
}