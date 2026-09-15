using Alexandria.Dto.Files.Streaming.Playlist;

namespace Alexandria.Common.Services;

public interface IAutoPlaylistSyncService
{
    /// <summary>
    /// Incrementally syncs every auto-playlist affected by one file: the groupings
    /// the file currently matches (creating the playlist row on first non-empty
    /// match) plus any auto-playlists still holding its items from a grouping it
    /// left. Desired sets exclude files without a resolvable transcode.
    /// </summary>
    Task<AutoPlaylistSyncResult> SyncFileAsync(Guid fileId, Guid ownerId, CancellationToken ct = default);

    /// <summary>
    /// Full reconcile of one owner's auto-playlists: every grouping bucket with a
    /// non-empty desired set gets its playlist (created on first match), every
    /// existing auto-playlist is re-diffed (reaps strays, emptied groups, deletes).
    /// This is what materializes playlists for libraries that predate all events.
    /// </summary>
    Task<AutoPlaylistSyncResult> SyncOwnerAsync(Guid ownerId, CancellationToken ct = default);
}