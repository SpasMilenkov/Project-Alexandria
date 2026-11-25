using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Exceptions.Streaming.Lyrics;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files.Streaming.Lyrics;
using Alexandria.Services.Streaming;
using AwesomeAssertions;
using NSubstitute;
using Microsoft.Extensions.Options;

namespace Alexandria.Tests.Unit.Lyrics;

public class TrackLyricsServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _jobId = Guid.NewGuid();
    private readonly ITrackLyricsRepository _lyricsRepo = Substitute.For<ITrackLyricsRepository>();
    private readonly IJobRepository _jobsRepo = Substitute.For<IJobRepository>();
    private readonly ITranspilationJobRepository _transcodes = Substitute.For<ITranspilationJobRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly IMessageBrokerStatus _broker = Substitute.For<IMessageBrokerStatus>();
    private readonly IAuditWriter _audit = Substitute.For<IAuditWriter>();
    private readonly Guid _fileId = Guid.NewGuid();

    private readonly TrackLyricsService _sut;
    private readonly ProcessingCapabilities _capabilities = new();

    public TrackLyricsServiceTests()
    {
        _uow.Lyrics.Returns(_lyricsRepo);
        _uow.Jobs.Returns(_jobsRepo);
        _uow.TranspilationJobs.Returns(_transcodes);
        _transcodes.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TranspilationJob, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new TranspilationJob { Id = _jobId, UserId = _userId });
        _jobsRepo.AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Job>());
        _broker.IsEnabled.Returns(true);
        _broker.IsAvailable.Returns(true);
        _transcodes.GetTrackFileAsync(_jobId, Arg.Any<CancellationToken>())
            .Returns(new TrackFileRef(Guid.NewGuid(), _fileId, "song.flac"));
        _sut = new TrackLyricsService(_uow, _outbox, _broker, _audit, Options.Create(_capabilities));
    }

    [Theory]
    [InlineData("get")]
    [InlineData("refetch")]
    [InlineData("change")]
    public async Task Lookup_lyrics_disabled_rejects_before_creating_or_resetting_jobs(string operation)
    {
        _capabilities.Lyrics = false;
        Func<Task> act = operation switch
        {
            "get" => () => _sut.GetLyricsForMediaAsync(_jobId, _userId, TestContext.Current.CancellationToken),
            "refetch" => () => _sut.QueueLyricsRefetchAsync(_jobId, _userId, TestContext.Current.CancellationToken),
            _ => () => _sut.ChangeProviderAsync(_jobId, _userId, LyricsProvider.LrclibPublic,
                TestContext.Current.CancellationToken)
        };

        await act.Should().ThrowAsync<CapabilityUnavailableException>();

        await _jobsRepo.DidNotReceive().AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
        _lyricsRepo.DidNotReceive().Update(Arg.Any<TrackLyrics>());
        await _outbox.DidNotReceiveWithAnyArgs().EnqueueJobAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetLyricsForMediaAsync_lookup_disabled_still_returns_existing_manual_lyrics()
    {
        _capabilities.Lyrics = false;
        _lyricsRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new TrackLyrics { PlainLyrics = "User lyrics", SourceProvider = LyricsProvider.Manual });

        var result = await _sut.GetLyricsForMediaAsync(_jobId, _userId, TestContext.Current.CancellationToken);

        result.PlayLyrics.Should().Be("User lyrics");
        await _outbox.DidNotReceiveWithAnyArgs().EnqueueJobAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UploadLyricsAsync_lookup_disabled_still_accepts_manual_lyrics()
    {
        _capabilities.Lyrics = false;

        await _sut.UploadLyricsAsync(_jobId, _userId, "User lyrics", null, TestContext.Current.CancellationToken);

        await _lyricsRepo.Received(1).AddAsync(Arg.Is<TrackLyrics>(l => l.SourceProvider == LyricsProvider.Manual),
            TestContext.Current.CancellationToken);
        await _outbox.DidNotReceiveWithAnyArgs().EnqueueJobAsync(default!, default, TestContext.Current.CancellationToken);
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
        Job? created = null;
        _jobsRepo.AddAsync(Arg.Do<Job>(j => created = j), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Job>());

        var result = await _sut.GetLyricsForMediaAsync(_jobId, _userId);

        result.Status.Should().Be(LyricsStatus.PendingFetch);
        created.Should().NotBeNull();
        created!.Type.Should().Be(JobType.LyricsFetch);
        await _outbox.Received(1).EnqueueJobAsync("lyrics.job", created.Id, Arg.Any<CancellationToken>());
    }

    // ---- QueueLyricsRefetchAsync ----

    [Fact]
    public async Task get_lyrics_not_found_broker_disabled_returns_pending_without_queueing()
    {
        _broker.IsEnabled.Returns(false);
        _broker.IsAvailable.Returns(false);

        var result = await _sut.GetLyricsForMediaAsync(_jobId, _userId, TestContext.Current.CancellationToken);

        result.Status.Should().Be(LyricsStatus.PendingFetch);
        await _jobsRepo.DidNotReceive().AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
        await _outbox.DidNotReceiveWithAnyArgs().EnqueueJobAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task queue_refetch_broker_outage_still_queues_through_the_outbox()
    {
        _broker.IsAvailable.Returns(false);
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((TrackLyrics?)null);
        _lyricsRepo.AddAsync(Arg.Any<TrackLyrics>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<TrackLyrics>());

        await _sut.QueueLyricsRefetchAsync(_jobId, _userId, TestContext.Current.CancellationToken);

        await _jobsRepo.Received(1).AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
        await _outbox.Received(1).EnqueueJobAsync("lyrics.job", Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task change_provider_broker_disabled_throws_before_any_write()
    {
        _broker.IsEnabled.Returns(false);
        _broker.IsAvailable.Returns(false);

        var act = () => _sut.ChangeProviderAsync(
            Guid.NewGuid(), _userId, LyricsProvider.LrclibPublic, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<MessageBrokerUnavailableException>())
            .Which.Disabled.Should().BeTrue();
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task queue_refetch_existing_non_manual_resets()
    {
        var lyricsId = Guid.NewGuid();
        var linkedJobId = Guid.NewGuid();
        var record = new TrackLyrics
        {
            Id = lyricsId,
            TranspilationJobId = _jobId,
            SourceProvider = LyricsProvider.LrclibPublic,
            Status = LyricsStatus.Fetched,
            FetchedAt = DateTime.UtcNow,
            JobId = linkedJobId
        };
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(record);
        _jobsRepo.TryRequeueFinishedAsync(linkedJobId, Arg.Any<CancellationToken>()).Returns(true);

        await _sut.QueueLyricsRefetchAsync(_jobId, _userId);

        record.Status.Should().Be(LyricsStatus.PendingFetch);
        record.FetchedAt.Should().BeNull();
        _lyricsRepo.Received(1).Update(record);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _jobsRepo.Received(1).TryRequeueFinishedAsync(linkedJobId, Arg.Any<CancellationToken>());
        await _outbox.Received(1).EnqueueJobAsync("lyrics.job", linkedJobId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task queue_refetch_while_the_fetch_is_queued_or_running_changes_nothing()
    {
        var linkedJobId = Guid.NewGuid();
        var record = new TrackLyrics
        {
            Id = Guid.NewGuid(),
            TranspilationJobId = _jobId,
            SourceProvider = LyricsProvider.LrclibPublic,
            Status = LyricsStatus.PendingFetch,
            JobId = linkedJobId
        };
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(record);
        _jobsRepo.TryRequeueFinishedAsync(linkedJobId, Arg.Any<CancellationToken>()).Returns(false);

        await _sut.QueueLyricsRefetchAsync(_jobId, _userId, TestContext.Current.CancellationToken);

        _lyricsRepo.DidNotReceiveWithAnyArgs().Update(default!);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _audit.DidNotReceiveWithAnyArgs().Record(default!);
        await _outbox.DidNotReceiveWithAnyArgs().EnqueueJobAsync(default!, Guid.Empty, TestContext.Current.CancellationToken);
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
        Job? created = null;
        _jobsRepo.AddAsync(Arg.Do<Job>(j => created = j), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Job>());

        await _sut.QueueLyricsRefetchAsync(_jobId, _userId);

        await _lyricsRepo.Received(1).AddAsync(
            Arg.Is<TrackLyrics>(l => l.TranspilationJobId == _jobId && l.Status == LyricsStatus.PendingFetch),
            Arg.Any<CancellationToken>());
        created.Should().NotBeNull();
        created!.Type.Should().Be(JobType.LyricsFetch);
        created.UserId.Should().Be(_userId);
        await _lyricsRepo.Received(1).AddAsync(
            Arg.Is<TrackLyrics>(l => l.JobId == created.Id),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _outbox.Received(1).EnqueueJobAsync("lyrics.job", created.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task queue_refetch_foreign_transcode_throws_without_insert()
    {
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((TrackLyrics?)null);
        _transcodes.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TranspilationJob, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((TranspilationJob?)null);

        var act = async () => await _sut.QueueLyricsRefetchAsync(_jobId, _userId);

        await act.Should().ThrowAsync<LyricsNotFoundException>();
        await _lyricsRepo.DidNotReceive().AddAsync(
            Arg.Any<TrackLyrics>(), Arg.Any<CancellationToken>());
        await _outbox.DidNotReceiveWithAnyArgs().EnqueueJobAsync(default!, default, TestContext.Current.CancellationToken);
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

        await _jobsRepo.Received(1).AddAsync(
            Arg.Is<Job>(j =>
                j.Status == JobStatus.Ready
                && j.ProgressPercent == 100
                && j.Type == JobType.LyricsFetch
                && j.UserId == _userId
                && j.CompletedAt != null),
            Arg.Any<CancellationToken>());
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
    public async Task upload_foreign_transcode_throws_without_insert()
    {
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((TrackLyrics?)null);
        _transcodes.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TranspilationJob, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((TranspilationJob?)null);

        var act = async () => await _sut.UploadLyricsAsync(_jobId, _userId, "plain", "synced");

        await act.Should().ThrowAsync<LyricsNotFoundException>();
        await _lyricsRepo.DidNotReceive().AddAsync(
            Arg.Any<TrackLyrics>(), Arg.Any<CancellationToken>());
        await _jobsRepo.DidNotReceive().AddAsync(
            Arg.Any<Job>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task upload_overwrites_existing()
    {
        var linkedJobId = Guid.NewGuid();
        var record = new TrackLyrics
        {
            Id = Guid.NewGuid(),
            TranspilationJob = new TranspilationJob { UserId = _userId },
            SourceProvider = LyricsProvider.LrclibPublic,
            Status = LyricsStatus.Fetched,
            JobId = linkedJobId
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
        await _jobsRepo.Received(1).UpdateStatusAsync(
            linkedJobId, JobStatus.Ready, Arg.Any<int?>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await _jobsRepo.Received(1).ClearErrorAsync(linkedJobId, Arg.Any<CancellationToken>());
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
        var linkedJobId = Guid.NewGuid();
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
            Cached = true,
            JobId = linkedJobId
        };
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(record);
        _jobsRepo.TryRequeueFinishedAsync(linkedJobId, Arg.Any<CancellationToken>()).Returns(true);

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
        await _jobsRepo.Received(1).TryRequeueFinishedAsync(linkedJobId, Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _outbox.Received(1).EnqueueJobAsync("lyrics.change", linkedJobId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task change_provider_while_the_fetch_is_running_throws_and_leaves_the_lyrics_alone()
    {
        var linkedJobId = Guid.NewGuid();
        var record = new TrackLyrics
        {
            Id = Guid.NewGuid(),
            TranspilationJob = new TranspilationJob { UserId = _userId },
            SourceProvider = LyricsProvider.LrclibPublic,
            Status = LyricsStatus.PendingFetch,
            JobId = linkedJobId
        };
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(record);
        _jobsRepo.TryRequeueFinishedAsync(linkedJobId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => _sut.ChangeProviderAsync(record.Id, _userId, LyricsProvider.Musicxmatch,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<LyricsFetchInProgressException>();
        record.SourceProvider.Should().Be(LyricsProvider.LrclibPublic);
        _lyricsRepo.DidNotReceiveWithAnyArgs().Update(default!);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _outbox.DidNotReceiveWithAnyArgs().EnqueueJobAsync(default!, Guid.Empty, TestContext.Current.CancellationToken);
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

    // ---- Audit ----

    [Fact]
    public async Task get_lyrics_first_view_queues_the_fetch_without_an_audit_row()
    {
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((TrackLyrics?)null);

        await _sut.GetLyricsForMediaAsync(_jobId, _userId, TestContext.Current.CancellationToken);

        await _lyricsRepo.Received(1).AddAsync(Arg.Any<TrackLyrics>(), Arg.Any<CancellationToken>());
        _audit.DidNotReceiveWithAnyArgs().Record(default!);
    }

    [Fact]
    public async Task queue_refetch_records_the_new_rows_id_against_the_tracks_file()
    {
        TrackLyrics? added = null;
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((TrackLyrics?)null);
        _lyricsRepo.AddAsync(Arg.Do<TrackLyrics>(l => added = l), Arg.Any<CancellationToken>());

        await _sut.QueueLyricsRefetchAsync(_jobId, _userId, TestContext.Current.CancellationToken);

        added.Should().NotBeNull();
        _audit.Received(1).Record(Arg.Is<AuditEntry>(e =>
            e.EventCode == AuditEventCode.LyricsRefetchQueued
            && e.EntityId == _fileId
            && e.EntityName == "song.flac"
            && Equals(e.Metadata!["lyrics_id"], added!.Id)));
    }

    [Fact]
    public async Task upload_new_lyrics_records_a_create_with_the_new_rows_id()
    {
        TrackLyrics? added = null;
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((TrackLyrics?)null);
        _lyricsRepo.AddAsync(Arg.Do<TrackLyrics>(l => added = l), Arg.Any<CancellationToken>());

        await _sut.UploadLyricsAsync(_jobId, _userId, "plain", null, TestContext.Current.CancellationToken);

        added.Should().NotBeNull();
        added!.Id.Should().NotBe(Guid.Empty);
        _audit.Received(1).Record(Arg.Is<AuditEntry>(e =>
            e.EventCode == AuditEventCode.LyricsUploaded
            && e.OperationType == OperationType.Create
            && Equals(e.Metadata!["lyrics_id"], added.Id)));
    }

    [Fact]
    public async Task upload_replacing_lyrics_records_an_update_with_the_existing_id()
    {
        var existing = new TrackLyrics
        {
            Id = Guid.NewGuid(),
            TranspilationJobId = _jobId,
            SourceProvider = LyricsProvider.LrclibPublic,
            JobId = Guid.NewGuid(),
        };
        _lyricsRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<TrackLyrics, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        await _sut.UploadLyricsAsync(_jobId, _userId, "plain", null, TestContext.Current.CancellationToken);

        _audit.Received(1).Record(Arg.Is<AuditEntry>(e =>
            e.OperationType == OperationType.Update && Equals(e.Metadata!["lyrics_id"], existing.Id)));
    }
}
