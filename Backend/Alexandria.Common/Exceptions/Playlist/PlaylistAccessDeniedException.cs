namespace Alexandria.Common.Exceptions.Playlist;

public sealed class PlaylistAccessDeniedException(Guid playlistId, Guid userId)
    : Exception($"User {userId} does not have access to playlist {playlistId}.");