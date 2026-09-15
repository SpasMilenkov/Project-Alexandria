using Alexandria.Dto.PlaylistStats;
using Alexandria.Dto.TranspilationStats;

namespace Alexandria.Common.Services;

public interface IPlaylistStatsService
{
    /// <summary>
    /// Current all-time status distribution plus duration statistics
    /// (avg / p50 / p90 minutes) over terminal playlist-sync jobs touching
    /// the last <paramref name="durationWindowDays"/>.
    /// </summary>
    Task<PlaylistOverviewResponse> GetOverviewAsync(int durationWindowDays,
        CancellationToken ct);

    /// <summary>
    /// Failure-rate and volume points bucketed by <paramref name="bucket"/>
    /// over the given UTC window. Playlist runs are binary, so the rate
    /// denominator counts Ready + Failed only; other job types sharing the
    /// window are excluded.
    /// </summary>
    Task<PlaylistTrendResponse> GetTrendAsync(DateTime fromUtc, DateTime toUtc,
        StatsBucket bucket, CancellationToken ct);
}