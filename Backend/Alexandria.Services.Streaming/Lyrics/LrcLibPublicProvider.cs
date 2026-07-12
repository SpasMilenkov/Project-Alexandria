using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Lyrics;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming.Lyrics;

public sealed partial class LrcLibPublicProvider(HttpClient http, ILogger<LrcLibPublicProvider> logger)
    : ITrackLyricsProvider
{
    private const string BaseUrl = "https://lrclib.net/api";


    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        // Public API requires no configuration, always available
        return Task.FromResult(true);
    }

    public LyricsProvider Provider => LyricsProvider.LrclibPublic;
    public int Priority => 10;

    public async Task<LyricsResult?> FetchAsync(
        string trackName,
        string? artistName,
        string? albumName,
        double? durationSeconds,
        CancellationToken ct = default)
    {
        var mainArtist = artistName is not null ? ExtractPrimaryArtist(artistName) : null;
        var normalizedTrack = NormalizeTitle(trackName);
        var normalizedArtist = artistName is not null ? NormalizeTitle(artistName) : null;

        // Step 1: strict match with all normalized metadata
        var result = await GetAsync(normalizedTrack, normalizedArtist, albumName, durationSeconds, ct);
        if (result is not null)
            return BuildResult(result, confidence: 10);

        // Step 2: search with original unnormalized track + artist, no album
        LogSearchFallback(trackName, artistName);
        result = await SearchAsync(trackName, artistName, durationSeconds, ct);
        if (result is not null)
            return BuildResult(result, confidence: 8);

        // Step 3: search with normalized track + main artist
        result = await GetAsync(trackName, mainArtist, albumName, durationSeconds, ct);
        if (result is not null)
            return BuildResult(result, confidence: 6);

        // Step 4: search with original track title + main artist
        result = await SearchAsync(normalizedTrack, artistName: mainArtist, durationSeconds, ct);
        if (result is not null)
            return BuildResult(result, confidence: 5);

        // Step 5: search with original track + main artist
        result = await GetAsync(normalizedTrack, mainArtist, albumName, durationSeconds, ct);
        if (result is not null)
            return BuildResult(result, confidence: 2);

        LogNotFound(trackName, artistName);
        return null;
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

    private static string NormalizeTitle(string input)
    {
        // Strip featuring artists: "Song (feat. Artist)" -> "Song"
        var withoutFeat =
            Regex.Replace(input, @"\s*[\(\[](feat|ft|with)\.?[^\)\]]*[\)\]]", "", RegexOptions.IgnoreCase);
        // Strip remaster/edition noise: "Song (2011 Remaster)" -> "Song"
        var withoutEdition = Regex.Replace(withoutFeat,
            @"\s*[\(\[](remaster|remastered|deluxe|edition|version|re-?issue)[^\)\]]*[\)\]]", "",
            RegexOptions.IgnoreCase);
        return withoutEdition.Trim();
    }

    private async Task<LrcLibResponse?> SearchAsync(
        string trackName,
        string? artistName,
        double? durationSeconds,
        CancellationToken ct)
    {
        var query = BuildQuery(trackName, artistName, albumName: null, durationSeconds: null);
        var url = $"{BaseUrl}/search?{query}";

        try
        {
            var response = await http.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            var results = await response.Content.ReadFromJsonAsync<List<LrcLibResponse>>(ct);

            if (results is null or { Count: 0 })
                return null;

            // Prefer the result with the closest duration when available
            return durationSeconds.HasValue
                ? results.MinBy(r => Math.Abs(r.Duration - durationSeconds.Value))
                : results.FirstOrDefault();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRequestFailed(url, ex);
            return null;
        }
    }

    private static string ExtractPrimaryArtist(string artist)
    {
        // Split on common multi-artist separators and take the first
        var separators = new[] { " feat.", " ft.", " featuring", " with ", " & ", ", ", " / ", ";" };
        foreach (var sep in separators)
        {
            var idx = artist.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                return artist[..idx].Trim();
        }

        return artist.Trim();
    }

    private static LyricsResult BuildResult(LrcLibResponse response, decimal confidence)
    {
        if (response.Instrumental)
            return new LyricsResult(null, null, response.Id.ToString(), Instrumental: true, LyricsProvider.LrclibPublic,
                confidence - 1);

        return new LyricsResult(
            response.PlainLyrics,
            response.SyncedLyrics,
            response.Id.ToString(),
            Instrumental: false,
            LyricsProvider.LrclibPublic,
            confidence
        );
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
            ["duration"] = durationSeconds.HasValue
                ? ((int)Math.Round(durationSeconds.Value)).ToString()
                : null
        };

        return string.Join("&", query
            .Where(kv => kv.Value is not null)
            .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value!)}"));
    }
}