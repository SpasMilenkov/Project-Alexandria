using Alexandria.Dto.Files.Streaming.Playlist;

namespace Alexandria.Services.Streaming;

/// <summary>
/// Pure reconciler diff for one auto-playlist. Desired is a set of
/// transcode job ids; current is every item row including soft-deletes:
/// <list type="bullet">
/// <item>live rows whose job left the desired set are removed (plain soft-delete,
/// reversible on re-match);</item>
/// <item>desired jobs with no row at all are added, unless a user tombstone
/// (<c>RemovedByUser</c>) exists for that job — those stay removed;</item>
/// <item>when a job-to-file map is provided, a user tombstone also blocks a
/// different desired job for the same file, so a transcode switch cannot
/// undo a removal;</item>
/// <item>desired jobs with only system-removed rows revive the newest tombstone
/// instead of duplicating;</item>
/// <item>extra live duplicates (user added the same track twice) are left alone.</item>
/// </list>
/// </summary>
internal static class PlaylistReconciler
{
    public static PlaylistDiff ComputeDiff(
        IReadOnlySet<Guid> desiredJobIds,
        IReadOnlyList<PlaylistItemState> items,
        IReadOnlyDictionary<Guid, Guid>? fileByJobId = null)
    {
        var liveByJob = new Dictionary<Guid, Guid>();
        var deadByJob = new Dictionary<Guid, PlaylistItemState>();
        var userRemovedJobs = new HashSet<Guid>();
        var userRemovedFiles = new HashSet<Guid>();

        foreach (var item in items)
        {
            if (!item.IsDeleted)
            {
                if (!liveByJob.ContainsKey(item.TranspilationJobId))
                    liveByJob[item.TranspilationJobId] = item.ItemId;
                continue;
            }

            if (item.RemovedByUser)
            {
                userRemovedJobs.Add(item.TranspilationJobId);
                if (fileByJobId is not null
                    && fileByJobId.TryGetValue(item.TranspilationJobId, out var removedFileId))
                    userRemovedFiles.Add(removedFileId);
                continue;
            }

            if (!deadByJob.TryGetValue(item.TranspilationJobId, out var kept)
                || Nullable.Compare(item.DeletedAt, kept.DeletedAt) > 0)
                deadByJob[item.TranspilationJobId] = item;
        }

        var add = new List<Guid>();
        var revive = new List<Guid>();
        var remove = new List<Guid>();

        foreach (var item in items)
        {
            if (!item.IsDeleted && !desiredJobIds.Contains(item.TranspilationJobId))
                remove.Add(item.ItemId);
        }

        foreach (var jobId in desiredJobIds)
        {
            if (liveByJob.ContainsKey(jobId))
                continue;
            if (userRemovedJobs.Contains(jobId))
                continue;
            if (fileByJobId is not null
                && fileByJobId.TryGetValue(jobId, out var desiredFileId)
                && userRemovedFiles.Contains(desiredFileId))
                continue;
            if (deadByJob.TryGetValue(jobId, out var tombstone))
                revive.Add(tombstone.ItemId);
            else
                add.Add(jobId);
        }

        return new PlaylistDiff(add, revive, remove);
    }
}