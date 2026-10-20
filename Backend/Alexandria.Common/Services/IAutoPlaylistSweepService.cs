using Alexandria.Dto.Files.Streaming.Playlist;

namespace Alexandria.Common.Services;

public interface IAutoPlaylistSweepService
{
    /// <summary>
    /// Reconciles auto-playlists for owners that need it. Layered after the enrichment
    /// backfill: desired sets only ever contain Ready-transcode files, so this
    /// never invents unplayable items, and files graduate from genre fallback to tag
    /// playlists through normal reconciliation once enrichment lands.
    /// </summary>
    /// <param name="forceFull">
    /// Reconciles every candidate owner, including owners whose sync markers are
    /// newer than their library changes. The worker uses this periodically as the
    /// repair backstop for changes no timestamp can observe.
    /// </param>
    Task<AutoPlaylistSweepResult> SweepAsync(bool forceFull = false, CancellationToken ct = default);
}