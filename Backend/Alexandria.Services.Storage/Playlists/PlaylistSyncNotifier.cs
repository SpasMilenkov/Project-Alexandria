using Alexandria.Common.Playlists;
using Alexandria.Common.Services;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Playlists;

/// <summary>
/// Shared fire-and-forget fan-out for file changes that affect auto-playlists.
/// A broker failure is logged, never thrown: playlist sync is a best-effort catch-up
/// and must not fail tag or metadata writes.
/// </summary>
internal static partial class PlaylistSyncNotifier
{
    public static async Task PublishFileChangedAsync(
        IPublisherService publisher,
        Guid fileId,
        Guid ownerId,
        ILogger logger)
    {
        try
        {
            await publisher.PublishAsync(
                PlaylistSyncMessage.Encode(fileId, ownerId),
                PlaylistSyncMessage.SyncRoutingKey);
        }
        catch (Exception ex)
        {
            LogSyncPublishFailed(logger, ex, fileId, ownerId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Failed to publish playlist sync for file {FileId} of owner {OwnerId}.")]
    private static partial void LogSyncPublishFailed(ILogger logger, Exception ex, Guid fileId, Guid ownerId);
}