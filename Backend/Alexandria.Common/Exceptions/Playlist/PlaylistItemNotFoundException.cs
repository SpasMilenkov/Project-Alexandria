namespace Alexandria.Common.Exceptions.Playlist;

public sealed class PlaylistItemNotFoundException(Guid id)
    : Exception($"Playlist item with id {id} was not found.");