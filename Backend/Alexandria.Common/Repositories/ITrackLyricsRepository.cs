using Alexandria.Data.Models;
using Alexandria.Dto.Files.Streaming.Lyrics;
using Alexandria.Dto.LyricsStats;

namespace Alexandria.Common.Repositories;

public interface ITrackLyricsRepository : IRepository<TrackLyrics>
{
    Task<TrackLyrics?> GetByJobIdAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>Current all-time row count per lyrics status.</summary>
    Task<IReadOnlyList<LyricsStatusCount>> GetStatusCountsAsync(CancellationToken ct = default);

    /// <summary>All-time attempt totals and failure counts per source provider.</summary>
    Task<IReadOnlyList<LyricsProviderBreakdownRow>> GetProviderBreakdownAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Average ConfidenceScore over successfully fetched rows; null when none.
    /// </summary>
    Task<double?> GetAvgConfidenceAsync(CancellationToken ct = default);

    /// <summary>Rows created inside the window, no tracking — trend bucketing input.</summary>
    Task<IReadOnlyList<TrackLyrics>> GetCreatedBetweenAsync(
        DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// Gets search data like name and duration for a song from the data inside the database
    /// </summary>
    /// <param name="lyricsId">The id of the tracklyrics record</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task<LyricsSearchParams?> GetSearchParamsAsync(Guid lyricsId, CancellationToken ct = default);
}