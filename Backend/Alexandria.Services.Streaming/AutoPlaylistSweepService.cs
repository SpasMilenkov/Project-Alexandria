using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Playlist;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public sealed partial class AutoPlaylistSweepService(
    IUnitOfWork unitOfWork,
    IAutoPlaylistSyncService syncService,
    ILogger<AutoPlaylistSweepService> logger) : IAutoPlaylistSweepService
{
    public async Task<AutoPlaylistSweepResult> SweepAsync(CancellationToken ct = default)
    {
        var ownerIds = await unitOfWork.Playlists.GetSyncOwnerIdsAsync(ct);

        var owners = 0;
        var synced = 0;
        var added = 0;
        var revived = 0;
        var removed = 0;

        foreach (var ownerId in ownerIds)
        {
            AutoPlaylistSyncResult result;
            try
            {
                result = await syncService.SyncOwnerAsync(ownerId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // The Failed run row is already recorded; keep sweeping the rest.
                LogOwnerSweepFailed(logger, ownerId, ex);
                continue;
            }

            owners++;
            synced += result.PlaylistsSynced;
            added += result.ItemsAdded;
            revived += result.ItemsRevived;
            removed += result.ItemsRemoved;
        }

        LogSweepCompleted(logger, owners, synced, added, revived, removed);

        return new AutoPlaylistSweepResult(owners, synced, added, revived, removed);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "Playlist sweep completed: {Owners} owner(s), {Playlists} playlist(s), {Added} added, {Revived} revived, {Removed} removed.")]
    private static partial void LogSweepCompleted(
        ILogger logger, int owners, int playlists, int added, int revived, int removed);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Playlist sweep skipped owner {OwnerId} after a sync failure; continuing with remaining owners.")]
    private static partial void LogOwnerSweepFailed(ILogger logger, Guid ownerId, Exception ex);
}