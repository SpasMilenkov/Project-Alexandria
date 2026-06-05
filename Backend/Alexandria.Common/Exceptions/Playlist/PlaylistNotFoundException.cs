namespace Alexandria.Common.Exceptions.Playlist;

public sealed class PlaylistNotFoundException(Guid id)
    : Exception($"Playlist with id {id} was not found.");