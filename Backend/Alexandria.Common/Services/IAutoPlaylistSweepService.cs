using Alexandria.Dto.Files.Streaming.Playlist;

namespace Alexandria.Common.Services;

public interface IAutoPlaylistSweepService
{
    /// <summary>
    /// Full reconcile of every owner's auto-playlists. Layered after the enrichment
    /// backfill: desired sets only ever contain Ready-transcode files, so this
    /// never invents unplayable items, and files graduate from genre fallback to tag
    /// playlists through normal reconciliation once enrichment lands.
    /// </summary>
    Task<AutoPlaylistSweepResult> SweepAsync(CancellationToken ct = default);
}