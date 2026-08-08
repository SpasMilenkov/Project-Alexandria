using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Exceptions.Streaming.Lyrics;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Lyrics;
using Alexandria.Services.Streaming.Lyrics;
using Alexandria.Workers.Lyrics.Handlers;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Alexandria.Tests.Unit.Lyrics;

public class LyricsHandlerTests
{
    private readonly Guid _lyricsId = Guid.NewGuid();
    private readonly ITrackLyricsRepository _lyricsRepo = Substitute.For<ITrackLyricsRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ITrackLyricsProvider _defaultProvider = Substitute.For<ITrackLyricsProvider>();
    private readonly LyricsHandler _sut;

    private readonly TrackLyrics _record = new()
    {
        Id = Guid.NewGuid(),
        SourceProvider = LyricsProvider.None,
        Status = LyricsStatus.PendingFetch
    };

    private readonly LyricsSearchParams _searchParams = new()
    {
        Name = "Test Song",
        Artist = "Test Artist",
        AlbumName = "Test Album",
        Duration = 240
    };

    private static readonly LyricsResult SampleResult = new(
        "plain lyric", "synced lyric", "42", Instrumental: false, LyricsProvider.LrclibPublic, 10m);

    public LyricsHandlerTests()
    {
        _uow.Lyrics.Returns(_lyricsRepo);
        _defaultProvider.Priority.Returns(10);
        _defaultProvider.Provider.Returns(LyricsProvider.LrclibPublic);
        _defaultProvider.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);

        var composite = new CompositeLyricsProvider(
            [_defaultProvider],
            Substitute.For<ILogger<CompositeLyricsProvider>>());

        _sut = new LyricsHandler(
            Substitute.For<ILogger<LyricsHandler>>(),
            composite,
            _uow);
    }

    [Fact]
    public async Task fetch_success_updates_record_with_lyrics()
    {
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(_record);
        _lyricsRepo.GetSearchParamsAsync(_lyricsId, Arg.Any<CancellationToken>())
            .Returns(_searchParams);
        _defaultProvider.FetchAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns(SampleResult);

        await _sut.HandleAsync(_lyricsId);

        _record.PlainLyrics.Should().Be("plain lyric");
        _record.SyncedLyrics.Should().Be("synced lyric");
        _record.ProviderTrackId.Should().Be("42");
        _record.SourceProvider.Should().Be(LyricsProvider.LrclibPublic);
        _record.IsInstrumental.Should().BeFalse();
        _record.Cached.Should().BeTrue();
        _record.Status.Should().Be(LyricsStatus.Fetched);
        _record.ConfidenceScore.Should().Be(10m);
        _record.FetchedAt.Should().NotBeNull();
        _lyricsRepo.Received(1).Update(_record);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task fetch_no_match_sets_status_to_no_match()
    {
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(_record);
        _lyricsRepo.GetSearchParamsAsync(_lyricsId, Arg.Any<CancellationToken>())
            .Returns(_searchParams);
        _defaultProvider.FetchAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns((LyricsResult?)null);

        await _sut.HandleAsync(_lyricsId);

        _record.Status.Should().Be(LyricsStatus.NoMatch);
        _record.FetchedAt.Should().NotBeNull();
        _lyricsRepo.Received(1).Update(_record);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task fetch_exception_sets_status_to_fetch_failed()
    {
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(_record);
        _lyricsRepo.GetSearchParamsAsync(_lyricsId, Arg.Any<CancellationToken>())
            .Returns(_searchParams);
        _defaultProvider.FetchAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns(SampleResult);

        var tcs = new TaskCompletionSource();
        tcs.SetException(new InvalidOperationException("DB error"));
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException(new InvalidOperationException("DB error")),
                _ => Task.CompletedTask);

        await _sut.HandleAsync(_lyricsId);

        _record.Status.Should().Be(LyricsStatus.FetchFailed);
        _lyricsRepo.Received(2).Update(_record);
        await _uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task lyrics_not_found_throws()
    {
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((TrackLyrics?)null);

        var act = async () => await _sut.HandleAsync(_lyricsId);

        await act.Should().ThrowAsync<LyricsNotFoundException>();
    }

    [Fact]
    public async Task search_params_not_found_throws()
    {
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(_record);
        _lyricsRepo.GetSearchParamsAsync(_lyricsId, Arg.Any<CancellationToken>())
            .Returns((LyricsSearchParams?)null);

        var act = async () => await _sut.HandleAsync(_lyricsId);

        await act.Should().ThrowAsync<LyricsNotFoundException>();
    }

    [Fact]
    public async Task uses_explicit_provider_when_source_provider_is_set()
    {
        _record.SourceProvider = LyricsProvider.LrclibPublic;

        var lrcLib = Substitute.For<ITrackLyricsProvider>();
        lrcLib.Priority.Returns(10);
        lrcLib.Provider.Returns(LyricsProvider.LrclibPublic);
        lrcLib.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        lrcLib.FetchAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns(SampleResult);

        var other = Substitute.For<ITrackLyricsProvider>();
        other.Priority.Returns(5);
        other.Provider.Returns(LyricsProvider.Musicxmatch);
        other.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);

        var composite = new CompositeLyricsProvider(
            [lrcLib, other],
            Substitute.For<ILogger<CompositeLyricsProvider>>());

        var sut = new LyricsHandler(
            Substitute.For<ILogger<LyricsHandler>>(),
            composite,
            _uow);

        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(_record);
        _lyricsRepo.GetSearchParamsAsync(_lyricsId, Arg.Any<CancellationToken>())
            .Returns(_searchParams);

        await sut.HandleAsync(_lyricsId);

        await lrcLib.Received(1).FetchAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
            Arg.Any<CancellationToken>());
        await other.DidNotReceiveWithAnyArgs().FetchAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
            Arg.Any<CancellationToken>());
        _record.Status.Should().Be(LyricsStatus.Fetched);
    }

    [Fact]
    public async Task passes_null_provider_when_source_provider_is_none()
    {
        _record.SourceProvider = LyricsProvider.None;

        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(_record);
        _lyricsRepo.GetSearchParamsAsync(_lyricsId, Arg.Any<CancellationToken>())
            .Returns(_searchParams);
        _defaultProvider.FetchAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns(SampleResult);

        await _sut.HandleAsync(_lyricsId);

        await _defaultProvider.Received(1).FetchAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
            Arg.Any<CancellationToken>());
        _record.Status.Should().Be(LyricsStatus.Fetched);
    }

    [Fact]
    public async Task handle_skips_unavailable_providers()
    {
        var musicxmatchResult = SampleResult with { Provider = LyricsProvider.Musicxmatch };
        var backup = Substitute.For<ITrackLyricsProvider>();
        backup.Priority.Returns(5);
        backup.Provider.Returns(LyricsProvider.Musicxmatch);
        backup.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        backup.FetchAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns(musicxmatchResult);

        _defaultProvider.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(false);

        var composite = new CompositeLyricsProvider(
            [_defaultProvider, backup],
            Substitute.For<ILogger<CompositeLyricsProvider>>());

        var sut = new LyricsHandler(
            Substitute.For<ILogger<LyricsHandler>>(),
            composite,
            _uow);

        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(_record);
        _lyricsRepo.GetSearchParamsAsync(_lyricsId, Arg.Any<CancellationToken>())
            .Returns(_searchParams);

        await sut.HandleAsync(_lyricsId);

        _record.SourceProvider.Should().Be(LyricsProvider.Musicxmatch);
        _record.Status.Should().Be(LyricsStatus.Fetched);
        _record.PlainLyrics.Should().Be("plain lyric");
    }
}