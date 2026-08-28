using Alexandria.Dto.LyricsStats;

namespace Alexandria.Common.Services;

public interface ILyricsStatsService
{
    /// <summary>
    /// All-time status distribution, per-provider attempt/failure totals and
    /// average confidence over successfully fetched rows.
    /// </summary>
    Task<LyricsOverviewResponse> GetOverviewAsync(CancellationToken ct);

    /// <summary>
    /// Attempt volume with fetch/failed splits bucketed by
    /// <paramref name="bucket"/> over the given UTC window. Rate denominator
    /// counts terminal rows only (Fetched + NoMatch + FetchFailed).
    /// </summary>
    Task<LyricsTrendResponse> GetTrendAsync(DateTime fromUtc, DateTime toUtc,
        LyricsStatsBucket bucket, CancellationToken ct);
}