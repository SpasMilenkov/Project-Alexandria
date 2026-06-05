using Alexandria.Common.Exceptions.Playlist;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming.Playlist;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public sealed partial class PlaylistService(
    IPlaylistRepository playlistRepo,
    ILogger<PlaylistService> logger) : IPlaylistService
{
    private readonly ILogger<PlaylistService> _logger = logger;

    public async Task<PlaylistDto> CreateAsync(string name,
        string? description,
        string? ambientTheme,
        bool hasCover,
        Guid[] initialTranspilationJobIds,
        Guid userId,
        CancellationToken ct = default)
    {
        LogCreatingPlaylist(name, userId);

        var items = initialTranspilationJobIds
            .Select((jobId, index) => new PlaylistItem
            {
                Id = Guid.NewGuid(),
                TranspilationJobId = jobId,
                Position = index
            })
            .ToList();

        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            AmbientTheme = ambientTheme,
            HasCover = hasCover,
            OwnerId = userId,
            PlaylistItems = items
        };

        var created = await playlistRepo.CreateAsync(playlist, ct);

        LogPlaylistCreated(created.Id, userId);

        return PlaylistDto.FromEntity(created);
    }

    public async Task DeleteAsync(Guid playlistId, Guid userId, CancellationToken ct = default)
    {
        LogDeletingPlaylist(playlistId, userId);

        if (!await playlistRepo.IsOwnerAsync(playlistId, userId, ct))
            throw new PlaylistAccessDeniedException(playlistId, userId);

        await playlistRepo.SoftDeleteAsync(playlistId, userId, ct);

        LogPlaylistDeleted(playlistId, userId);
    }

    public async Task<PlaylistDto> UpdateAsync(
        Guid playlistId,
        string? name,
        string? description,
        bool? hasCover,
        string? ambientTheme,
        Guid userId,
        CancellationToken ct = default)
    {
        LogUpdatingPlaylist(playlistId, userId);

        var playlist = await playlistRepo.GetByIdAsync(playlistId, ct)
                       ?? throw new PlaylistNotFoundException(playlistId);

        if (playlist.OwnerId != userId)
            throw new PlaylistAccessDeniedException(playlistId, userId);

        if (name is not null)
            playlist.Name = name;

        if (description is not null)
            playlist.Description = description;

        if (hasCover is not null)
            playlist.HasCover = hasCover.Value;

        if (ambientTheme is not null)
            playlist.AmbientTheme = ambientTheme;

        playlist.UpdatedBy = userId;

        await playlistRepo.UpdateAsync(playlist, ct);

        LogPlaylistUpdated(playlistId, userId);

        return await playlistRepo.GetSummaryAsync(playlistId, userId, ct)
               ?? throw new PlaylistNotFoundException(playlistId);
    }

    public async Task<PlaylistDetailDto> GetByIdAsync(Guid playlistId, Guid userId, CancellationToken ct = default)
    {
        LogFetchingPlaylist(playlistId, userId);

        return await playlistRepo.GetWithItemsAsync(playlistId, userId, ct)
               ?? throw new PlaylistNotFoundException(playlistId);
    }

    public async Task<PaginatedResult<PlaylistDto>> GetByUserAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        LogFetchingUserPlaylists(userId, page, pageSize);
        return await playlistRepo.GetByUserAsync(userId, page, pageSize, ct);
    }

    public async Task ReorderItemsAsync(
        Guid playlistId, Guid[] orderedItemIds, Guid userId, CancellationToken ct = default)
    {
        LogReorderingItems(playlistId, userId);

        if (!await playlistRepo.IsOwnerAsync(playlistId, userId, ct))
            throw new PlaylistAccessDeniedException(playlistId, userId);

        await playlistRepo.ReorderItemsAsync(playlistId, orderedItemIds, ct);

        LogItemsReordered(playlistId, userId);
    }

    public async Task RemoveItemAsync(Guid playlistId, Guid itemId, Guid userId, CancellationToken ct = default)
    {
        LogRemovingItem(itemId, playlistId, userId);

        if (!await playlistRepo.IsOwnerAsync(playlistId, userId, ct))
            throw new PlaylistAccessDeniedException(playlistId, userId);

        await playlistRepo.RemoveItemAsync(playlistId, itemId, userId, ct);

        LogItemRemoved(itemId, playlistId, userId);
    }

    public async Task<PlaylistItemDto> AddItemAsync(
        Guid playlistId, Guid transpilationJobId, Guid userId, CancellationToken ct = default)
    {
        LogAddingItem(transpilationJobId, playlistId, userId);

        if (!await playlistRepo.IsOwnerAsync(playlistId, userId, ct))
            throw new PlaylistAccessDeniedException(playlistId, userId);

        var item = new PlaylistItem
        {
            Id = Guid.NewGuid(),
            PlaylistId = playlistId,
            TranspilationJobId = transpilationJobId
        };

        var result = await playlistRepo.AddItemAsync(item, ct);

        LogItemAdded(result.Id, playlistId, userId);

        return result;
    }
}