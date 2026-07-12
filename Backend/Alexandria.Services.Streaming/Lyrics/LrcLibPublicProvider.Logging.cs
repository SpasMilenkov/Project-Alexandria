using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming.Lyrics;

public sealed partial class LrcLibPublicProvider
{
    [LoggerMessage(Level = LogLevel.Debug,
        Message = "lrclib /get miss for '{TrackName}' by '{ArtistName}', falling back to search")]
    private partial void LogSearchFallback(string trackName, string? artistName);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "lrclib found no lyrics for '{TrackName}' by '{ArtistName}'")]
    private partial void LogNotFound(string trackName, string? artistName);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "'{TrackName}' by '{ArtistName}' is instrumental, skipping lyrics")]
    private partial void LogInstrumental(string trackName, string? artistName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "lrclib request failed for '{Url}'")]
    private partial void LogRequestFailed(string url, Exception ex);
}