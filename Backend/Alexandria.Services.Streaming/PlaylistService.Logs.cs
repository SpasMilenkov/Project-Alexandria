using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public sealed partial class PlaylistService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Creating playlist '{Name}' for user {UserId}")]
    private partial void LogCreatingPlaylist(string name, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Playlist {PlaylistId} created for user {UserId}")]
    private partial void LogPlaylistCreated(Guid playlistId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleting playlist {PlaylistId} for user {UserId}")]
    private partial void LogDeletingPlaylist(Guid playlistId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Playlist {PlaylistId} deleted for user {UserId}")]
    private partial void LogPlaylistDeleted(Guid playlistId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updating playlist {PlaylistId} for user {UserId}")]
    private partial void LogUpdatingPlaylist(Guid playlistId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Playlist {PlaylistId} updated for user {UserId}")]
    private partial void LogPlaylistUpdated(Guid playlistId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Fetching playlist {PlaylistId} for user {UserId}")]
    private partial void LogFetchingPlaylist(Guid playlistId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Fetching playlists page {Page} (size {PageSize}) for user {UserId}")]
    private partial void LogFetchingUserPlaylists(Guid userId, int page, int pageSize);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Reordering items in playlist {PlaylistId} for user {UserId}")]
    private partial void LogReorderingItems(Guid playlistId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Items reordered in playlist {PlaylistId} for user {UserId}")]
    private partial void LogItemsReordered(Guid playlistId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Removing item {ItemId} from playlist {PlaylistId} for user {UserId}")]
    private partial void LogRemovingItem(Guid itemId, Guid playlistId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Item {ItemId} removed from playlist {PlaylistId} for user {UserId}")]
    private partial void LogItemRemoved(Guid itemId, Guid playlistId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Adding file {FileId} to playlist {PlaylistId} for user {UserId}")]
    private partial void LogAddingItem(Guid fileId, Guid playlistId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Item {ItemId} added to playlist {PlaylistId} for user {UserId}")]
    private partial void LogItemAdded(Guid itemId, Guid playlistId, Guid userId);
}