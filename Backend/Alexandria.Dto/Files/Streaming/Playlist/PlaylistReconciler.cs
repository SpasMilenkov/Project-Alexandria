namespace Alexandria.Dto.Files.Streaming.Playlist;

/// <summary>
/// One playlist item as the reconciler sees it, including soft-deleted rows.
/// Tombstones with <see cref="RemovedByUser"/> block re-adds; plain soft-deletes
/// revive on re-match. Live rows with <see cref="AddedByUser"/> are user pins and
/// survive removal sweeps.
/// </summary>
public sealed record PlaylistItemState(
    Guid ItemId,
    Guid TranspilationJobId,
    bool IsDeleted,
    bool RemovedByUser,
    bool AddedByUser,
    DateTime? DeletedAt);

/// <summary>
/// Reconciler diff for one playlist: job ids to insert, tombstone item ids to
/// undelete, live item ids to soft-delete. Empty means already in sync.
/// </summary>
public sealed record PlaylistDiff(
    IReadOnlyList<Guid> AddJobIds,
    IReadOnlyList<Guid> ReviveItemIds,
    IReadOnlyList<Guid> RemoveItemIds);

/// <summary>One playlist's reconciler diff inside an owner-wide apply batch.</summary>
public sealed record AutoPlaylistDiff(Guid PlaylistId, PlaylistDiff Diff);

/// <summary>Sync outcome for one file across all its affected playlists.</summary>
public sealed record AutoPlaylistSyncResult(
    int PlaylistsSynced,
    int ItemsAdded,
    int ItemsRevived,
    int ItemsRemoved);