using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Lyrics;
using Alexandria.Services.Streaming.Lyrics;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Alexandria.Tests.Unit.Lyrics;

public class CompositeLyricsProviderTests
{
    private static readonly LyricsResult SampleResult = new(
        "plain", "synced", "1", Instrumental: false, LyricsProvider.LrclibPublic, 10m);

    private static CompositeLyricsProvider CreateSut(params ITrackLyricsProvider[] providers)
    {
        return new CompositeLyricsProvider(
            providers,
            Substitute.For<ILogger<CompositeLyricsProvider>>());
    }

    [Fact]
    public async Task all_providers_unavailable_returns_null()
    {
        var p1 = Substitute.For<ITrackLyricsProvider>();
        p1.Priority.Returns(10);
        p1.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(false);

        var p2 = Substitute.For<ITrackLyricsProvider>();
        p2.Priority.Returns(5);
        p2.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(false);

        var sut = CreateSut(p1, p2);

        var result = await sut.FetchAsync("Song", "Artist", null, null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task first_provider_succeeds_returns_immediately()
    {
        var p1 = Substitute.For<ITrackLyricsProvider>();
        p1.Priority.Returns(10);
        p1.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        p1.FetchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns(SampleResult);

        var p2 = Substitute.For<ITrackLyricsProvider>();
        p2.Priority.Returns(5);

        var sut = CreateSut(p1, p2);

        var result = await sut.FetchAsync("Song", "Artist", null, null);

        result.Should().Be(SampleResult);
        await p2.DidNotReceiveWithAnyArgs().IsAvailableAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task first_unavailable_second_succeeds()
    {
        var p1 = Substitute.For<ITrackLyricsProvider>();
        p1.Priority.Returns(10);
        p1.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(false);

        var p2 = Substitute.For<ITrackLyricsProvider>();
        p2.Priority.Returns(5);
        p2.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        p2.FetchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns(SampleResult);

        var sut = CreateSut(p1, p2);

        var result = await sut.FetchAsync("Song", "Artist", null, null);

        result.Should().Be(SampleResult);
    }

    [Fact]
    public async Task all_providers_return_null()
    {
        var p1 = Substitute.For<ITrackLyricsProvider>();
        p1.Priority.Returns(10);
        p1.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        p1.FetchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns((LyricsResult?)null);

        var p2 = Substitute.For<ITrackLyricsProvider>();
        p2.Priority.Returns(5);
        p2.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        p2.FetchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns((LyricsResult?)null);

        var sut = CreateSut(p1, p2);

        var result = await sut.FetchAsync("Song", "Artist", null, null, ct: TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task first_times_out_second_succeeds()
    {
        var p1 = Substitute.For<ITrackLyricsProvider>();
        p1.Priority.Returns(10);
        p1.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        p1.FetchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<LyricsResult?>(new OperationCanceledException()));

        var p2 = Substitute.For<ITrackLyricsProvider>();
        p2.Priority.Returns(5);
        p2.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        p2.FetchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns(SampleResult);

        var sut = CreateSut(p1, p2);

        var result = await sut.FetchAsync("Song", "Artist", null, null, ct: TestContext.Current.CancellationToken);

        result.Should().Be(SampleResult);
    }

    [Fact]
    public async Task first_throws_second_succeeds()
    {
        var p1 = Substitute.For<ITrackLyricsProvider>();
        p1.Priority.Returns(10);
        p1.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        p1.FetchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<LyricsResult?>(new InvalidOperationException("boom")));

        var p2 = Substitute.For<ITrackLyricsProvider>();
        p2.Priority.Returns(5);
        p2.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        p2.FetchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns(SampleResult);

        var sut = CreateSut(p1, p2);

        var result = await sut.FetchAsync("Song", "Artist", null, null, ct: TestContext.Current.CancellationToken);

        result.Should().Be(SampleResult);
    }

    [Fact]
    public async Task target_provider_restricts_to_that_provider()
    {
        var p1 = Substitute.For<ITrackLyricsProvider>();
        p1.Priority.Returns(10);
        p1.Provider.Returns(LyricsProvider.LrclibPublic);
        p1.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        p1.FetchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns(SampleResult);

        var p2 = Substitute.For<ITrackLyricsProvider>();
        p2.Priority.Returns(5);
        p2.Provider.Returns(LyricsProvider.Musicxmatch);

        var sut = CreateSut(p1, p2);

        var result = await sut.FetchAsync("Song", "Artist", null, null, LyricsProvider.LrclibPublic,
            TestContext.Current.CancellationToken);

        result.Should().Be(SampleResult);
        await p2.DidNotReceiveWithAnyArgs().IsAvailableAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task target_provider_not_registered_falls_back_to_all()
    {
        var p1 = Substitute.For<ITrackLyricsProvider>();
        p1.Priority.Returns(10);
        p1.Provider.Returns(LyricsProvider.LrclibPublic);
        p1.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        p1.FetchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns(SampleResult);

        var sut = CreateSut(p1);

        var result = await sut.FetchAsync("Song", "Artist", null, null, LyricsProvider.Musicxmatch,
            TestContext.Current.CancellationToken);

        result.Should().Be(SampleResult);
    }

    [Fact]
    public async Task target_provider_unavailable_returns_null()
    {
        var p1 = Substitute.For<ITrackLyricsProvider>();
        p1.Priority.Returns(10);
        p1.Provider.Returns(LyricsProvider.LrclibPublic);
        p1.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(false);

        var sut = CreateSut(p1);

        var result = await sut.FetchAsync("Song", "Artist", null, null, LyricsProvider.LrclibPublic,
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task providers_ordered_by_descending_priority()
    {
        var high = Substitute.For<ITrackLyricsProvider>();
        high.Priority.Returns(50);

        var mid = Substitute.For<ITrackLyricsProvider>();
        mid.Priority.Returns(10);

        var low = Substitute.For<ITrackLyricsProvider>();
        low.Priority.Returns(1);

        var sut = CreateSut(low, high, mid);
        high.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        mid.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        low.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);

        _ = await sut.FetchAsync("Song", null, null, null, ct: TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            high.IsAvailableAsync(Arg.Any<CancellationToken>());
            mid.IsAvailableAsync(Arg.Any<CancellationToken>());
            low.IsAvailableAsync(Arg.Any<CancellationToken>());
        });
    }
}