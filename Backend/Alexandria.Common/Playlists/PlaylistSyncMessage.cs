using System.Text;
using System.Text.Json;

namespace Alexandria.Common.Playlists;

/// <summary>
/// File-scoped playlist sync request: the file that changed plus its owner, so the
/// worker can resolve affected groupings without trusting caller-computed keys.
/// JSON body over UTF-8 bytes. Pure parse so publishers, drainer, and tests share
/// one definition.
/// </summary>
public static class PlaylistSyncMessage
{
    public const string SyncRoutingKey = "playlist.sync";

    public static byte[] Encode(Guid fileId, Guid ownerId) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Payload(fileId, ownerId)));

    public static bool TryParse(byte[] body, out Guid fileId, out Guid ownerId)
    {
        fileId = Guid.Empty;
        ownerId = Guid.Empty;

        Payload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(Encoding.UTF8.GetString(body));
        }
        catch (Exception ex) when (ex is DecoderFallbackException or JsonException)
        {
            return false;
        }

        if (payload is null || payload.FileId == Guid.Empty || payload.OwnerId == Guid.Empty)
            return false;

        fileId = payload.FileId;
        ownerId = payload.OwnerId;
        return true;
    }

    private sealed record Payload(Guid FileId, Guid OwnerId);
}