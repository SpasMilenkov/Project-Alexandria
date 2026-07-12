using Alexandria.Data.Models;
using Alexandria.Dto.Files.Streaming.Lyrics;

namespace Alexandria.Common.Repositories;

public interface ITrackLyricsRepository : IRepository<TrackLyrics>
{
    /// <summary>
    /// Gets search data like name and duration for a song from the data inside the database
    /// </summary>
    /// <param name="lyricsId">The id of the tracklyrics record</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task<LyricsSearchParams?> GetSearchParamsAsync(Guid lyricsId, CancellationToken ct = default);
}