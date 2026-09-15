using Alexandria.Dto.Files.Streaming.Playlist;

namespace Alexandria.Common.Services;

public interface IPlaylistJobResolver
{
    /// <summary>
    /// Resolves every streamable file of one owner to its playlist-bindable transcode
    /// job. Fetch-once, decided in memory. Files without a
    /// viable transcode are absent from the result.
    /// </summary>
    Task<IReadOnlyList<PlaylistJobResolution>> ResolveForOwnerAsync(
        Guid ownerId, CancellationToken ct = default);
}