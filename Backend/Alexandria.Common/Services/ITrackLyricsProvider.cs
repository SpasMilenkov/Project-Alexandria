using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Lyrics;

namespace Alexandria.Common.Services;

public interface ITrackLyricsProvider
{
    LyricsProvider Provider { get; }
    public int Priority { get; }

    Task<LyricsResult?> FetchAsync(
        string trackName,
        string? artistName,
        string? albumName,
        double? durationSeconds,
        CancellationToken ct = default);

    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}