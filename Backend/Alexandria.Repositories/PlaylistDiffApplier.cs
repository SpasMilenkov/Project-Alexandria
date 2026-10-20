using Alexandria.Data.Models;
using Alexandria.Dto.Files.Streaming.Playlist;

namespace Alexandria.Repositories;

/// <summary>
/// Applies one reconciler diff to a playlist's tracked item rows in memory.
/// New rows append after the live maximum; a revived system tombstone keeps its
/// original position when it is still free and appends otherwise. Position
/// bookkeeping advances with every kept or appended row so a batch never leaves
/// two live rows on the same position.
/// </summary>
internal static class PlaylistDiffApplier
{
    public static IReadOnlyList<PlaylistItem> Apply(
        Guid playlistId,
        List<PlaylistItem> items,
        PlaylistDiff diff,
        Guid ownerId,
        DateTime now)
    {
        var byId = items.ToDictionary(i => i.Id);

        var maxLivePosition = items
            .Where(i => i.DeletedAt is null)
            .Select(i => i.Position)
            .DefaultIfEmpty(-1)
            .Max();

        var newItems = new List<PlaylistItem>(diff.AddJobIds.Count);

        foreach (var jobId in diff.AddJobIds)
        {
            var position = maxLivePosition + 1;

            var item = new PlaylistItem
            {
                Id = Guid.NewGuid(),
                PlaylistId = playlistId,
                TranspilationJobId = jobId,
                Position = position,
                CreatedAt = now,
                UpdatedBy = ownerId,
            };

            items.Add(item);
            newItems.Add(item);
            maxLivePosition = position;
        }

        foreach (var itemId in diff.ReviveItemIds)
        {
            if (!byId.TryGetValue(itemId, out var item) || item.DeletedAt is null)
                continue;

            var position = item.Position;

            var occupied = items.Any(i =>
                i.Id != item.Id && i.DeletedAt is null && i.Position == position);

            if (occupied)
                position = maxLivePosition + 1;

            item.Position = position;
            item.DeletedAt = null;
            item.UpdatedAt = now;

            if (position > maxLivePosition)
                maxLivePosition = position;
        }

        foreach (var itemId in diff.RemoveItemIds)
        {
            if (!byId.TryGetValue(itemId, out var item) || item.DeletedAt is not null)
                continue;

            item.DeletedAt = now;
            item.UpdatedAt = now;
            item.UpdatedBy = ownerId;
        }

        return newItems;
    }
}