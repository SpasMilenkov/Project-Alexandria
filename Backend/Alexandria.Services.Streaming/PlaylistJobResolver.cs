using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Playlist;

namespace Alexandria.Services.Streaming;

public sealed class PlaylistJobResolver(ITranspilationJobRepository jobs) : IPlaylistJobResolver
{
    public async Task<IReadOnlyList<PlaylistJobResolution>> ResolveForOwnerAsync(
        Guid ownerId, CancellationToken ct = default)
    {
        var rows = await jobs.GetResolvableJobsAsync(ownerId, ct);

        var resolutions = new List<PlaylistJobResolution>();

        foreach (var group in rows.GroupBy(r => r.FileId))
        {
            var candidates = group.Select(r =>
                new PlaylistJobCandidate(r.JobId, r.VersionId, r.CompletedAt));

            var winner = PlaylistJobSelection.SelectJob(
                candidates, group.First().CurrentVersionId);

            if (winner.HasValue)
                resolutions.Add(new PlaylistJobResolution(group.Key, winner.Value));
        }

        return resolutions;
    }
}