namespace Alexandria.Dto.Files.Streaming.Playlist;

/// <summary>Sweep outcome across all swept owners.</summary>
public sealed record AutoPlaylistSweepResult(
    int OwnersSwept,
    int PlaylistsSynced,
    int ItemsAdded,
    int ItemsRevived,
    int ItemsRemoved);