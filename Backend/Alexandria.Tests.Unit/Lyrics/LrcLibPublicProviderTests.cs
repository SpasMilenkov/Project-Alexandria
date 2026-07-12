using System.Net;
using System.Text.Json;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Lyrics;
using Alexandria.Services.Streaming.Lyrics;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Alexandria.Tests.Unit.Lyrics;

public class LrcLibPublicProviderTests
{
    private sealed class Responder : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _onSend;
        public Responder(Func<HttpRequestMessage, HttpResponseMessage> onSend) => _onSend = onSend;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_onSend(request));
    }

    private static LrcLibPublicProvider CreateProvider(Func<HttpRequestMessage, HttpResponseMessage> onSend)
    {
        var client = new HttpClient(new Responder(onSend));
        return new LrcLibPublicProvider(client, Substitute.For<ILogger<LrcLibPublicProvider>>());
    }

    private static LrcLibPublicProvider CreateProviderWithTracker(
        List<HttpRequestMessage> captured,
        Func<HttpRequestMessage, HttpResponseMessage>? onSend = null)
    {
        onSend ??= _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        return CreateProvider(req =>
        {
            captured.Add(req);
            return onSend(req);
        });
    }

    private static HttpResponseMessage JsonResponse(object body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body))
        };
    }

    private static readonly LrcLibResponse SampleResponse = new(
        Id: 12345,
        TrackName: "Test Song",
        ArtistName: "Test Artist",
        AlbumName: "Test Album",
        Duration: 240.5,
        Instrumental: false,
        PlainLyrics: "Line 1\nLine 2",
        SyncedLyrics: "[00:01.00] Line 1\n[00:02.00] Line 2"
    );

    // ---- GET endpoint tests ----

    [Fact]
    public async Task get_hit_returns_lyrics()
    {
        var provider = CreateProvider(_ => JsonResponse(SampleResponse));

        var result = await provider.FetchAsync("Test Song", "Test Artist", "Test Album", 240.5);

        result.Should().NotBeNull();
        result!.PlainLyrics.Should().Be("Line 1\nLine 2");
        result.SyncedLyrics.Should().Be("[00:01.00] Line 1\n[00:02.00] Line 2");
        result.ProviderTrackId.Should().Be("12345");
        result.Instrumental.Should().BeFalse();
        result.Provider.Should().Be(LyricsProvider.LrclibPublic);
        result.ConfidenceScore.Should().Be(10m);
    }

    [Fact]
    public async Task get_404_returns_null()
    {
        var provider = CreateProvider(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await provider.FetchAsync("Unknown", null, null, null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task get_http_error_returns_null()
    {
        var provider = CreateProvider(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await provider.FetchAsync("Error", null, null, null);

        result.Should().BeNull();
    }

    // ---- SEARCH endpoint tests ----

    [Fact]
    public async Task search_with_results_picks_closest_duration()
    {
        var calls = 0;
        var provider = CreateProvider(req =>
        {
            calls++;
            if (req.RequestUri!.AbsolutePath.Contains("/get"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            var results = new[]
            {
                SampleResponse with { Duration = 100, Id = 1 },
                SampleResponse with { Duration = 200, Id = 2 },
                SampleResponse with { Duration = 300, Id = 3 },
            };
            return JsonResponse(results);
        });

        var result = await provider.FetchAsync("Test Song", "Test Artist", "Test Album", 210);

        result.Should().NotBeNull();
        result!.ProviderTrackId.Should().Be("2");
    }

    [Fact]
    public async Task search_empty_returns_null()
    {
        var provider = CreateProvider(req =>
            req.RequestUri!.AbsolutePath.Contains("/get")
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : JsonResponse(Array.Empty<object>()));

        var result = await provider.FetchAsync("Unknown", null, null, null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task search_404_returns_null()
    {
        var provider = CreateProvider(req =>
            new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await provider.FetchAsync("Unknown", null, null, null);

        result.Should().BeNull();
    }

    // ---- Fallback strategy tests ----

    [Fact]
    public async Task step1_strict_match_returns_immediately()
    {
        var captured = new List<HttpRequestMessage>();
        var provider = CreateProviderWithTracker(captured, _ => JsonResponse(SampleResponse));

        var result = await provider.FetchAsync("Song", "Artist", "Album", 200);

        result.Should().NotBeNull();
        result!.ConfidenceScore.Should().Be(10m);
        captured.Should().HaveCount(1);
        captured[0].RequestUri!.AbsolutePath.Should().EndWith("/get");
    }

    [Fact]
    public async Task step1_miss_step2_search_succeeds()
    {
        var captured = new List<HttpRequestMessage>();
        var provider = CreateProviderWithTracker(captured, req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/get"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            return JsonResponse(new[] { SampleResponse });
        });

        var result = await provider.FetchAsync("Song", "Artist", "Album", 200);

        result.Should().NotBeNull();
        result!.ConfidenceScore.Should().Be(8m);
        captured[0].RequestUri!.AbsolutePath.Should().EndWith("/get");
        captured[1].RequestUri!.AbsolutePath.Should().EndWith("/search");
    }

    [Fact]
    public async Task all_steps_miss_returns_null()
    {
        var provider = CreateProvider(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await provider.FetchAsync("Song", "Artist", "Album", 200);

        result.Should().BeNull();
    }

    // ---- Instrumental ----

    [Fact]
    public async Task instrumental_track_returns_instrumental_flag()
    {
        var instrumental = SampleResponse with { Instrumental = true, PlainLyrics = null, SyncedLyrics = null };
        var provider = CreateProvider(_ => JsonResponse(instrumental));

        var result = await provider.FetchAsync("Track", "Artist", null, null);

        result.Should().NotBeNull();
        result!.Instrumental.Should().BeTrue();
        result.PlainLyrics.Should().BeNull();
        result.SyncedLyrics.Should().BeNull();
        result.ConfidenceScore.Should().Be(9m);
    }

    // ---- Normalization verification (indirect via URL capture) ----

    [Fact]
    public async Task normalize_strips_feat_from_track_name()
    {
        var captured = new List<HttpRequestMessage>();
        var provider = CreateProviderWithTracker(captured);

        await provider.FetchAsync("Song (feat. Other)", "Artist", null, null);

        var url = captured[0].RequestUri!.Query;
        url.Should().Contain("track_name=Song");
        url.Should().NotContain("feat");
    }

    [Fact]
    public async Task normalize_strips_remastered_from_track_name()
    {
        var captured = new List<HttpRequestMessage>();
        var provider = CreateProviderWithTracker(captured);

        await provider.FetchAsync("Song (Remastered)", "Artist", null, null);

        var url = captured[0].RequestUri!.Query;
        url.Should().Contain("track_name=Song");
        url.Should().NotContain("Remastered");
    }

    [Fact]
    public async Task extract_primary_artist_uses_first_name_before_feat()
    {
        var captured = new List<HttpRequestMessage>();
        var provider = CreateProviderWithTracker(captured);

        await provider.FetchAsync("Song", "Main feat. Other", null, null);

        var url = captured[0].RequestUri!.Query;
        url.Should().Contain("artist_name=Main");
    }

    [Fact]
    public async Task extract_primary_artist_uses_full_name_when_no_separator()
    {
        var captured = new List<HttpRequestMessage>();
        var provider = CreateProviderWithTracker(captured);

        await provider.FetchAsync("Song", "Solo Artist", null, null);

        var url = captured[0].RequestUri!.Query;
        url.Should().Contain("artist_name=Solo%20Artist");
    }

    [Fact]
    public async Task normalize_also_applies_to_artist_name()
    {
        var captured = new List<HttpRequestMessage>();
        var provider = CreateProviderWithTracker(captured);

        await provider.FetchAsync("Song", "Artist (Remaster)", null, null);

        var url = captured[0].RequestUri!.Query;
        url.Should().Contain("artist_name=Artist");
        url.Should().NotContain("Remaster");
    }

    // ---- Basic properties ----

    [Fact]
    public void provider_enum_is_lrclib_public()
    {
        var provider = CreateProvider(_ => new HttpResponseMessage());
        provider.Provider.Should().Be(LyricsProvider.LrclibPublic);
    }

    [Fact]
    public void priority_is_10()
    {
        var provider = CreateProvider(_ => new HttpResponseMessage());
        provider.Priority.Should().Be(10);
    }

    [Fact]
    public async Task is_available_returns_true()
    {
        var provider = CreateProvider(_ => new HttpResponseMessage());
        var available = await provider.IsAvailableAsync();
        available.Should().BeTrue();
    }
}