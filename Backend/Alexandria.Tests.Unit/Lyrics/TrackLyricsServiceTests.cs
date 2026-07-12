using System.Linq.Expressions;
using System.Text;
using Alexandria.Common;
using Alexandria.Common.Exceptions.Streaming.Lyrics;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Streaming;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Lyrics;

public class TrackLyricsServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _jobId = Guid.NewGuid();
    private readonly ITrackLyricsRepository _lyricsRepo = Substitute.For<ITrackLyricsRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IPublisherService _publisher = Substitute.For<IPublisherService>();

    private readonly TrackLyricsService _sut;

    public TrackLyricsServiceTests()
    {
        _uow.Lyrics.Returns(_lyricsRepo);
        _sut = new TrackLyricsService(_uow, _publisher);
    }

    // ---- GetLyricsForMediaAsync ----

    [Fact]
    public async Task get_lyrics_existing_returns_dto()
    {
        var lyricsId = Guid.NewGuid();
        var record = new TrackLyrics
        {
            Id = lyricsId,
            Status = LyricsStatus.Fetched,
            SourceProvider = LyricsProvider.LrclibPublic,
            PlainLyrics = "hello",
            TranspilationJobId = _jobId
        };
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(record);

        var result = await _sut.GetLyricsForMediaAsync(_jobId, _userId);

        result.Id.Should().Be(lyricsId);
        result.Status.Should().Be(LyricsStatus.Fetched);
        result.PlayLyrics.Should().Be("hello");
    }

    [Fact]
    public async Task get_lyrics_not_found_triggers_refetch()
    {
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((TrackLyrics?)null);
        _lyricsRepo.AddAsync(Arg.Any<TrackLyrics>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<TrackLyrics>());

        var result = await _sut.GetLyricsForMediaAsync(_jobId, _userId);

        result.Status.Should().Be(LyricsStatus.PendingFetch);
        await _publisher.ReceivedWithAnyArgs().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
    }

    // ---- QueueLyricsRefetchAsync ----

    [Fact]
    public async Task queue_refetch_existing_non_manual_resets()
    {
        var lyricsId = Guid.NewGuid();
        var record = new TrackLyrics
        {
            Id = lyricsId,
            TranspilationJobId = _jobId,
            SourceProvider = LyricsProvider.LrclibPublic,
            Status = LyricsStatus.Fetched,
            FetchedAt = DateTime.UtcNow
        };
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(record);

        await _sut.QueueLyricsRefetchAsync(_jobId, _userId);

        record.Status.Should().Be(LyricsStatus.PendingFetch);
        record.FetchedAt.Should().BeNull();
        _lyricsRepo.Received(1).Update(record);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(
            Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b) == lyricsId.ToString()),
            "lyrics.job");
    }

    [Fact]
    public async Task queue_refetch_existing_manual_throws()
    {
        var record = new TrackLyrics
        {
            Id = Guid.NewGuid(),
            TranspilationJobId = _jobId,
            SourceProvider = LyricsProvider.Manual,
            Status = LyricsStatus.Fetched
        };
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(record);

        var act = async () => await _sut.QueueLyricsRefetchAsync(_jobId, _userId);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task queue_refetch_no_existing_creates_new()
    {
        var newId = Guid.NewGuid();
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((TrackLyrics?)null);
        _lyricsRepo.AddAsync(Arg.Any<TrackLyrics>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var entity = callInfo.Arg<TrackLyrics>();
                entity.Id = newId;
                return entity;
            });

        await _sut.QueueLyricsRefetchAsync(_jobId, _userId);

        await _lyricsRepo.Received(1).AddAsync(
            Arg.Is<TrackLyrics>(l => l.TranspilationJobId == _jobId && l.Status == LyricsStatus.PendingFetch),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(
            Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b) == newId.ToString()),
            "lyrics.job");
    }

    // ---- RemoveLyricsAsync ----

    [Fact]
    public async Task remove_lyrics_found_soft_deletes()
    {
        var lyricsId = Guid.NewGuid();
        var record = new TrackLyrics { Id = lyricsId, TranspilationJob = new TranspilationJob { UserId = _userId } };
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(record);

        await _sut.RemoveLyricsAsync(lyricsId, _userId);

        record.DeletedAt.Should().NotBeNull();
        _lyricsRepo.Received(1).Update(record);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task remove_lyrics_not_found_throws()
    {
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((TrackLyrics?)null);

        var act = async () => await _sut.RemoveLyricsAsync(Guid.NewGuid(), _userId);

        await act.Should().ThrowAsync<LyricsNotFoundException>();
    }

    // ---- UploadLyricsAsync ----

    [Fact]
    public async Task upload_new_creates_with_manual_provider()
    {
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((TrackLyrics?)null);

        await _sut.UploadLyricsAsync(_jobId, _userId, "plain", "synced");

        await _lyricsRepo.Received(1).AddAsync(
            Arg.Is<TrackLyrics>(l =>
                l.PlainLyrics == "plain" &&
                l.SyncedLyrics == "synced" &&
                l.SourceProvider == LyricsProvider.Manual &&
                l.Status == LyricsStatus.Fetched),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task upload_overwrites_existing()
    {
        var record = new TrackLyrics
        {
            Id = Guid.NewGuid(),
            TranspilationJob = new TranspilationJob { UserId = _userId },
            SourceProvider = LyricsProvider.LrclibPublic,
            Status = LyricsStatus.Fetched
        };
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(record);

        await _sut.UploadLyricsAsync(_jobId, _userId, "new plain", "new synced");

        record.PlainLyrics.Should().Be("new plain");
        record.SyncedLyrics.Should().Be("new synced");
        record.SourceProvider.Should().Be(LyricsProvider.Manual);
        record.Status.Should().Be(LyricsStatus.Fetched);
        record.Cached.Should().BeFalse();
        record.ProviderTrackId.Should().BeNull();
        record.ConfidenceScore.Should().BeNull();
        _lyricsRepo.Received(1).Update(record);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task upload_both_null_throws()
    {
        var act = async () => await _sut.UploadLyricsAsync(_jobId, _userId, null, null);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ---- ChangeProviderAsync ----

    [Fact]
    public async Task change_provider_valid_resets_and_publishes()
    {
        var lyricsId = Guid.NewGuid();
        var record = new TrackLyrics
        {
            Id = lyricsId,
            TranspilationJob = new TranspilationJob { UserId = _userId },
            SourceProvider = LyricsProvider.LrclibPublic,
            Status = LyricsStatus.Fetched,
            PlainLyrics = "old",
            SyncedLyrics = "old",
            ProviderTrackId = "42",
            ConfidenceScore = 10m,
            FetchedAt = DateTime.UtcNow,
            Cached = true
        };
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(record);

        await _sut.ChangeProviderAsync(lyricsId, _userId, LyricsProvider.Musicxmatch);

        record.SourceProvider.Should().Be(LyricsProvider.Musicxmatch);
        record.Status.Should().Be(LyricsStatus.PendingFetch);
        record.PlainLyrics.Should().BeNull();
        record.SyncedLyrics.Should().BeNull();
        record.ProviderTrackId.Should().BeNull();
        record.ConfidenceScore.Should().BeNull();
        record.FetchedAt.Should().BeNull();
        record.Cached.Should().BeFalse();
        _lyricsRepo.Received(1).Update(record);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(
            Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b) == lyricsId.ToString()),
            "lyrics.change");
    }

    [Fact]
    public async Task change_provider_manual_throws()
    {
        var act = async () => await _sut.ChangeProviderAsync(Guid.NewGuid(), _userId, LyricsProvider.Manual);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task change_provider_not_found_throws()
    {
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((TrackLyrics?)null);

        var act = async () => await _sut.ChangeProviderAsync(Guid.NewGuid(), _userId, LyricsProvider.LrclibPublic);

        await act.Should().ThrowAsync<LyricsNotFoundException>();
    }
}