using Alexandria.Dto.Files.Streaming.Playlist;

namespace Alexandria.Common.Services;

public interface IAutoPlaylistGroupingService
{
    /// <summary>
    /// Read-only grouping views over one owner's live files: normalized artist and
    /// Artist+Album buckets, per-tag buckets, and genre fallback for tag-less files.
    /// Fetch-once from the repository, bucketed in memory.
    /// </summary>
    Task<AutoPlaylistGroupingResult> GetGroupingAsync(Guid ownerId, CancellationToken ct = default);
}