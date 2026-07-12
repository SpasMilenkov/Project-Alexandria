using Alexandria.Data.Models;
using Alexandria.Dto.Files.Streaming.Lyrics;

namespace Alexandria.Dto.Extensions;

public static class LyricsExtensions
{
    public static LyricsDto ToDto(this TrackLyrics trackLyrics) =>
        new LyricsDto
        {
            Id = trackLyrics.Id,
            Status = trackLyrics.Status,
            Provider = trackLyrics.SourceProvider,
            Cached = trackLyrics.Cached,
            ConfidenceScore = trackLyrics.ConfidenceScore,
            FetchedAt = trackLyrics.FetchedAt,
            PlayLyrics = trackLyrics.PlainLyrics,
            SyncedLyrics = trackLyrics.SyncedLyrics
        };
}