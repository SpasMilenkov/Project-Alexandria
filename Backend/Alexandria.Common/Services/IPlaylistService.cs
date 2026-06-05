using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming.Playlist;

namespace Alexandria.Common.Services;

public interface IPlaylistService
{
    Task<PlaylistDto> CreateAsync(
        string name,
        string? description,
        string? ambientTheme,
        bool hasCover,
        Guid[] initialTranspilationJobIds,
        Guid userId,
        CancellationToken ct = default);

    Task DeleteAsync(Guid playlistId, Guid userId, CancellationToken ct = default);

    Task<PlaylistDto> UpdateAsync(
        Guid playlistId,
        string? name,
        string? description,
        bool? hasCover,
        string? ambientTheme,
        Guid userId,
        CancellationToken ct = default);

    Task<PlaylistDetailDto> GetByIdAsync(Guid playlistId, Guid userId, CancellationToken ct = default);

    Task<PaginatedResult<PlaylistDto>> GetByUserAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default);

    Task ReorderItemsAsync(Guid playlistId, Guid[] orderedItemIds, Guid userId, CancellationToken ct = default);

    Task RemoveItemAsync(Guid playlistId, Guid itemId, Guid userId, CancellationToken ct = default);

    Task<PlaylistItemDto> AddItemAsync(
        Guid playlistId, Guid transpilationJobId, Guid userId, CancellationToken ct = default);
}