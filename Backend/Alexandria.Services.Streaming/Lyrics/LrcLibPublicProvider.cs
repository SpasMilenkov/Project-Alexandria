using System.Net;
using System.Net.Http.Json;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Lyrics;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming.Lyrics;

public sealed partial class LrcLibPublicProvider(HttpClient http, ILogger<LrcLibPublicProvider> logger)
    : ITrackLyricsProvider
{
    private const string BaseUrl = "https://lrclib.net/api";

    public Task<bool> IsAvailableAsync(Guid jobId, CancellationToken ct = default)
    {
        // Public API requires no configuration, always available
        return Task.FromResult(true);
    }

    public async Task<LyricsResult?> FetchAsync(
        string trackName,
        string? artistName,
        string? albumName,
        double? durationSeconds,
        CancellationToken ct = default)
    {
        var result = await GetAsync(trackName, artistName, albumName, durationSeconds, ct);

        if (result is null)
        {
            LogSearchFallback(trackName, artistName);
            result = await SearchAsync(trackName, artistName, albumName, ct);
        }

        if (result is null)
        {
            LogNotFound(trackName, artistName);
            return null;
        }

        if (!result.Instrumental)
            return new LyricsResult(
                result.PlainLyrics,
                result.SyncedLyrics,
                result.Id.ToString(),
                Instrumental: false
            );
        LogInstrumental(trackName, artistName);
        return new LyricsResult(null, null, result.Id.ToString(), Instrumental: true);

    }

    private async Task<LrcLibResponse?> GetAsync(
        string trackName,
        string? artistName,
        string? albumName,
        double? durationSeconds,
        CancellationToken ct)
    {
        var query = BuildQuery(trackName, artistName, albumName, durationSeconds);
        var url = $"{BaseUrl}/get?{query}";

        try
        {
            var response = await http.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<LrcLibResponse>(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRequestFailed(url, ex);
            return null;
        }
    }

    private async Task<LrcLibResponse?> SearchAsync(
        string trackName,
        string? artistName,
        string? albumName,
        CancellationToken ct)
    {
        var query = BuildQuery(trackName, artistName, albumName, durationSeconds: null);
        var url = $"{BaseUrl}/search?{query}";

        try
        {
            var response = await http.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            var results = await response.Content
                .ReadFromJsonAsync<List<LrcLibResponse>>(ct);

            return results?.FirstOrDefault();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRequestFailed(url, ex);
            return null;
        }
    }

    private static string BuildQuery(
        string trackName,
        string? artistName,
        string? albumName,
        double? durationSeconds)
    {
        var query = new Dictionary<string, string?>
        {
            ["track_name"] = trackName,
            ["artist_name"] = artistName,
            ["album_name"] = albumName,
            ["duration"] = durationSeconds?.ToString("F0")
        };

        return string.Join("&", query
            .Where(kv => kv.Value is not null)
            .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value!)}"));
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "lrclib /get miss for '{TrackName}' by '{ArtistName}', falling back to search")]
    private partial void LogSearchFallback(string trackName, string? artistName);

    [LoggerMessage(Level = LogLevel.Information, Message = "lrclib found no lyrics for '{TrackName}' by '{ArtistName}'")]
    private partial void LogNotFound(string trackName, string? artistName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "'{TrackName}' by '{ArtistName}' is instrumental, skipping lyrics")]
    private partial void LogInstrumental(string trackName, string? artistName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "lrclib request failed for '{Url}'")]
    private partial void LogRequestFailed(string url, Exception ex);
}