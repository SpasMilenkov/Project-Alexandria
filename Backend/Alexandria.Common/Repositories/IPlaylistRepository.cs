using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming.Playlist;

namespace Alexandria.Common.Repositories;

/// <summary>Repository for <see cref="Playlist"/> persistence operations.</summary>
public interface IPlaylistRepository : IRepository<Playlist>
{
    /// <summary>Creates and persists a new playlist, cascading timestamps to any initial items.</summary>
    Task<Playlist> CreateAsync(Playlist entity, CancellationToken ct = default);

    /// <summary>Persists changes to an existing playlist and returns the updated instance.</summary>
    Task<Playlist> UpdateAsync(Playlist entity, CancellationToken ct = default);

    /// <summary>Returns a summary projection with a live item count. Used after mutations.</summary>
    Task<PlaylistDto?> GetSummaryAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Returns the playlist with its ordered non-deleted items projected to a detail DTO,
    /// including the representation list for each item resolved via the
    /// PlaylistItem -> TranspilationJob -> FileVersion -> File chain.
    /// </summary>
    Task<PlaylistDetailDto?> GetWithItemsAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Returns a paginated list of playlists owned by the given user.</summary>
    Task<PaginatedResult<PlaylistDto>> GetByUserAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Soft-deletes the playlist and cascades the deletion to all its items atomically.</summary>
    Task SoftDeleteAsync(Guid playlistId, Guid userId, CancellationToken ct = default);

    /// <summary>Updates each item's Position according to the provided ordered ID sequence.</summary>
    Task ReorderItemsAsync(Guid playlistId, Guid[] orderedItemIds, CancellationToken ct = default);

    /// <summary>
    /// Appends an item at the next available position and returns the projected DTO,
    /// resolving file name, MIME type, segment prefix and representations from
    /// the TranspilationJob chain.
    /// </summary>
    Task<PlaylistItemDto> AddItemAsync(PlaylistItem item, CancellationToken ct = default);

    /// <summary>Soft-deletes a single playlist item.</summary>
    Task RemoveItemAsync(Guid playlistId, Guid itemId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Returns a random Preview.Path sourced from the files that back the given transpilation jobs.
    /// Returns null when none of those files have a non-deleted preview.
    /// </summary>
    Task<string?> GetRandomPreviewPathForJobsAsync(Guid[] jobIds, CancellationToken ct = default);

    /// <summary>Returns whether the given user is the owner of the playlist and it has not been deleted.</summary>
    Task<bool> IsOwnerAsync(Guid playlistId, Guid userId, CancellationToken ct = default);
}