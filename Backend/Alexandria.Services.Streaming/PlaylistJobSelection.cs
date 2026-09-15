using Alexandria.Dto.Files.Streaming.Playlist;

namespace Alexandria.Services.Streaming;

/// <summary>
/// Pure version-selection rule: the current version's Ready job wins even when
/// older; otherwise the most recently completed job wins; no candidate means the file
/// is skipped (never a nullable-FileId fallback). A Ready job without
/// <c>CompletedAt</c> is anomalous but still resolvable, sorting oldest.
/// </summary>
internal static class PlaylistJobSelection
{
    public static Guid? SelectJob(
        IEnumerable<PlaylistJobCandidate> candidates,
        Guid? currentVersionId)
    {
        PlaylistJobCandidate? currentBest = null;
        PlaylistJobCandidate? overallBest = null;

        foreach (var candidate in candidates)
        {
            if (IsMoreRecent(candidate, overallBest))
                overallBest = candidate;

            if (currentVersionId.HasValue && candidate.VersionId == currentVersionId.Value &&
                IsMoreRecent(candidate, currentBest))
            {
                currentBest = candidate;
            }
        }

        if (currentBest is not null)
            return currentBest.JobId;

        return overallBest?.JobId;
    }

    private static bool IsMoreRecent(PlaylistJobCandidate candidate, PlaylistJobCandidate? best)
    {
        if (best is null)
            return true;
        return SortKey(candidate.CompletedAt) > SortKey(best.CompletedAt);
    }

    private static DateTime SortKey(DateTime? completedAt)
    {
        if (completedAt.HasValue)
            return completedAt.Value;
        return DateTime.MinValue;
    }
}