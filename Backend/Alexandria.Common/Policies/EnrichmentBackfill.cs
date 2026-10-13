using System.Linq.Expressions;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Common.Policies;

/// <summary>
/// Candidate filtering for files that predate an AutoTag rule or scope change.
/// </summary>
public static class EnrichmentBackfill
{
    /// <summary>
    /// Routing key for JSON backfill requests carrying policy id, rule id, and cutoff.
    /// </summary>
    public const string BackfillRoutingKey = "media-metadata.enrich-backfill";

    /// <summary>
    /// Live files sitting directly in one of
    /// <paramref name="directoryIds"/>, created strictly before
    /// <paramref name="predatingCutoff"/> (the rule or scope change timestamp).
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
