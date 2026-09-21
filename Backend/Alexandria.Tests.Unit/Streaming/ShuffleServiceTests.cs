using Alexandria.Common;
using Alexandria.Common.Exceptions.Playlist;
using Alexandria.Common.Exceptions.Streaming;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Dto.Files.Streaming.Shuffle;
using Alexandria.Services.Streaming.Shuffle;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Alexandria.Tests.Unit.Streaming;

public class ShuffleServiceTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }

    private readonly Guid _owner = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private readonly TestTimeProvider _time = new(Start);
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IStreamHistoryRepository _histories = Substitute.For<IStreamHistoryRepository>();
    private readonly IPlaylistRepository _playlists = Substitute.For<IPlaylistRepository>();

    private IShuffleService CreateSut()
    {
        _unitOfWork.Files.Returns(_files);
        _unitOfWork.StreamingHistories.Returns(_histories);
        _unitOfWork.Playlists.Returns(_playlists);
        _playlists.IsOwnerAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var store = new ShuffleSessionStore(_time, Substitute.For<ILogger<ShuffleSessionStore>>());
        return new ShuffleService(
            _unitOfWork, store, _time, Substitute.For<ILogger<ShuffleService>>());
    }

    private static List<ShuffleCandidate> Candidates(int count, Guid? playlistId = null) =>
        Enumerable.Range(0, count)
            .Select(i => new ShuffleCandidate
            {
                Entry = new PlaybackSourceEntryRef
                {
                    FileId = Guid.Parse($"10000000-0000-0000-0000-0000000000{i:00}"),
                    PlaylistItemId = playlistId.HasValue
                        ? Guid.Parse($"20000000-0000-0000-0000-0000000000{i:00}")
                        : null,
                    TranspilationJobId = Guid.NewGuid(),
                },
                DurationSeconds = 120.0,
            })
            .ToList();

    private static MediaFileDto ToDto(PlaybackSourceEntryRef entry) => new()
    {
        FileId = entry.FileId,
        FileName = $"file-{entry.FileId}",
        MimeType = "audio/mpeg",
        CurrentVersionId = Guid.NewGuid(),
        Duration = 120.0,
        TranspilationJobId = entry.TranspilationJobId ?? Guid.NewGuid(),
        PlaylistItemId = entry.PlaylistItemId,
        IsVideo = false,
        SegmentPrefix = "seg",
    };

    private void HydrationMirrorsRefs()
    {
        _files.GetPlaybackEntriesAsync(
                Arg.Any<Guid>(),
                Arg.Any<PlaybackSourceDto>(),
                Arg.Any<IReadOnlyList<PlaybackSourceEntryRef>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<IReadOnlyList<PlaybackSourceEntryRef>>(2).Select(ToDto).ToList());
    }

    private static CreateShuffleSessionCommand Command(
        Guid requestId, Guid? playlistId = null, int limit = 50) => new()
    {
        RequestId = requestId,
        Source = new PlaybackSourceDto { IsVideo = false, PlaylistId = playlistId },
        Limit = limit,
    };

    [Fact]
    public async Task Create_returns_whole_source_with_absolute_positions()
    {
        var sut = CreateSut();
        HydrationMirrorsRefs();
        _files.GetShuffleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>())
            .Returns(Candidates(5));
        _histories.GetShuffleListenRowsAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<ShuffleListenRow>());

        var response = await sut.CreateSessionAsync(
            _owner, Command(Guid.NewGuid()), TestContext.Current.CancellationToken);

        response.TotalCount.Should().Be(5);
        response.AnchorPosition.Should().BeNull();
        response.Offset.Should().Be(0);
        response.ScannedCount.Should().Be(5);
        response.NextOffset.Should().BeNull();
        response.Items.Should().HaveCount(5);
        response.Items.Select(i => i.Position).Should().Equal(0, 1, 2, 3, 4);
        response.Items.Select(i => i.File.FileId).Should().OnlyHaveUniqueItems();
        response.AlgorithmVersion.Should().Be(1);
    }

    [Fact]
    public async Task Create_with_video_skips_history_read()
    {
        var sut = CreateSut();
        HydrationMirrorsRefs();
        _files.GetShuffleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>())
            .Returns(Candidates(3));

        var command = Command(Guid.NewGuid()) with
        {
            Source = new PlaybackSourceDto { IsVideo = true, PlaylistId = null },
        };
        await sut.CreateSessionAsync(_owner, command, TestContext.Current.CancellationToken);

        _histories.DidNotReceive().GetShuffleListenRowsAsync(
            Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_hydrates_only_the_requested_batch()
    {
        var sut = CreateSut();
        HydrationMirrorsRefs();
        _files.GetShuffleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>())
            .Returns(Candidates(10));
        _histories.GetShuffleListenRowsAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<ShuffleListenRow>());

        var response = await sut.CreateSessionAsync(
            _owner, Command(Guid.NewGuid(), limit: 3), TestContext.Current.CancellationToken);

        response.TotalCount.Should().Be(10);
        response.Items.Should().HaveCount(3);
        response.ScannedCount.Should().Be(3);
        response.NextOffset.Should().Be(3);
        await _files.Received(1).GetPlaybackEntriesAsync(
            _owner,
            Arg.Any<PlaybackSourceDto>(),
            Arg.Is<IReadOnlyList<PlaybackSourceEntryRef>>(refs => refs.Count == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_with_foreign_playlist_throws_without_reading_source()
    {
        var sut = CreateSut();
        var playlistId = Guid.NewGuid();
        _playlists.IsOwnerAsync(playlistId, _owner, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => sut.CreateSessionAsync(
            _owner, Command(Guid.NewGuid(), playlistId), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<PlaylistNotFoundException>();
        await _files.DidNotReceive().GetShuffleCandidatesAsync(
            Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_with_missing_anchor_throws_conflict()
    {
        var sut = CreateSut();
        HydrationMirrorsRefs();
        _files.GetShuffleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>())
            .Returns(Candidates(3));
        _histories.GetShuffleListenRowsAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<ShuffleListenRow>());

        var command = Command(Guid.NewGuid());
        command = command with { AnchorFileId = Guid.NewGuid() };

        var act = () => sut.CreateSessionAsync(_owner, command, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ShuffleAnchorConflictException>();
    }

    [Fact]
    public async Task Get_preserves_gaps_and_derives_continuation_from_scanned_slots()
    {
        var sut = CreateSut();
        var candidates = Candidates(5);
        _files.GetShuffleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>())
            .Returns(candidates);
        _histories.GetShuffleListenRowsAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<ShuffleListenRow>());

        _files.GetPlaybackEntriesAsync(
                Arg.Any<Guid>(),
                Arg.Any<PlaybackSourceDto>(),
                Arg.Any<IReadOnlyList<PlaybackSourceEntryRef>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<IReadOnlyList<PlaybackSourceEntryRef>>(2).Select(ToDto).ToList());

        var created = await sut.CreateSessionAsync(
            _owner, Command(Guid.NewGuid()), TestContext.Current.CancellationToken);

        var droppedFile = created.Items[2].File.FileId;
        _files.GetPlaybackEntriesAsync(
                Arg.Any<Guid>(),
                Arg.Any<PlaybackSourceDto>(),
                Arg.Any<IReadOnlyList<PlaybackSourceEntryRef>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<IReadOnlyList<PlaybackSourceEntryRef>>(2)
                .Where(r => r.FileId != droppedFile).Select(ToDto).ToList());

        var ct = TestContext.Current.CancellationToken;
        var page = await sut.GetSessionBatchAsync(_owner, created.SessionId, 0, 5, ct);

        page.Items.Select(i => i.Position).Should().Equal(0, 1, 3, 4);
        page.ScannedCount.Should().Be(5);
        page.NextOffset.Should().BeNull();
        page.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task Get_at_snapshot_end_returns_terminal_empty_response()
    {
        var sut = CreateSut();
        HydrationMirrorsRefs();
        _files.GetShuffleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>())
            .Returns(Candidates(4));
        _histories.GetShuffleListenRowsAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<ShuffleListenRow>());

        var ct = TestContext.Current.CancellationToken;
        var created = await sut.CreateSessionAsync(_owner, Command(Guid.NewGuid()), ct);
        var page = await sut.GetSessionBatchAsync(_owner, created.SessionId, 4, 50, ct);

        page.Items.Should().BeEmpty();
        page.ScannedCount.Should().Be(0);
        page.NextOffset.Should().BeNull();
        page.TotalCount.Should().Be(4);
    }

    [Fact]
    public async Task Get_reports_expiry_twenty_four_hours_after_last_access()
    {
        var sut = CreateSut();
        HydrationMirrorsRefs();
        _files.GetShuffleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>())
            .Returns(Candidates(1));
        _histories.GetShuffleListenRowsAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<ShuffleListenRow>());
        var ct = TestContext.Current.CancellationToken;
        var created = await sut.CreateSessionAsync(_owner, Command(Guid.NewGuid()), ct);
        _time.Advance(TimeSpan.FromHours(2));

        var read = await sut.GetSessionBatchAsync(_owner, created.SessionId, 0, 1, ct);

        read.ExpiresAt.Should().Be(Start.AddHours(26));
    }

    [Fact]
    public async Task Get_beyond_snapshot_end_throws()
    {
        var sut = CreateSut();
        HydrationMirrorsRefs();
        _files.GetShuffleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>())
            .Returns(Candidates(4));
        _histories.GetShuffleListenRowsAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<ShuffleListenRow>());

        var ct = TestContext.Current.CancellationToken;
        var created = await sut.CreateSessionAsync(_owner, Command(Guid.NewGuid()), ct);

        var act = () => sut.GetSessionBatchAsync(_owner, created.SessionId, 5, 50, ct);

        await act.Should().ThrowAsync<ShuffleOffsetOutOfRangeException>();
    }

    [Fact]
    public async Task Get_does_not_rebuild_the_cycle()
    {
        var sut = CreateSut();
        HydrationMirrorsRefs();
        _files.GetShuffleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>())
            .Returns(Candidates(6));
        _histories.GetShuffleListenRowsAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<ShuffleListenRow>());

        var ct = TestContext.Current.CancellationToken;
        var created = await sut.CreateSessionAsync(_owner, Command(Guid.NewGuid()), ct);
        var first = await sut.GetSessionBatchAsync(_owner, created.SessionId, 0, 6, ct);
        var second = await sut.GetSessionBatchAsync(_owner, created.SessionId, 0, 6, ct);

        first.Items.Select(i => i.File.FileId).Should().Equal(second.Items.Select(i => i.File.FileId));
        await _files.Received(1).GetShuffleCandidatesAsync(
            Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_replay_returns_identical_session_without_rebuilding()
    {
        var sut = CreateSut();
        HydrationMirrorsRefs();
        _files.GetShuffleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>())
            .Returns(Candidates(4));
        _histories.GetShuffleListenRowsAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<ShuffleListenRow>());

        var ct = TestContext.Current.CancellationToken;
        var requestId = Guid.NewGuid();
        var first = await sut.CreateSessionAsync(_owner, Command(requestId), ct);
        var second = await sut.CreateSessionAsync(_owner, Command(requestId), ct);

        second.SessionId.Should().Be(first.SessionId);
        second.Items.Select(i => i.File.FileId).Should().Equal(first.Items.Select(i => i.File.FileId));
        await _files.Received(1).GetShuffleCandidatesAsync(
            Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_conflicting_fingerprint_throws()
    {
        var sut = CreateSut();
        HydrationMirrorsRefs();
        _files.GetShuffleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>())
            .Returns(Candidates(4));
        _histories.GetShuffleListenRowsAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<ShuffleListenRow>());

        var ct = TestContext.Current.CancellationToken;
        var requestId = Guid.NewGuid();
        await sut.CreateSessionAsync(_owner, Command(requestId, limit: 10), ct);

        var act = () => sut.CreateSessionAsync(_owner, Command(requestId, limit: 20), ct);

        await act.Should().ThrowAsync<ShuffleRequestConflictException>();
    }

    [Fact]
    public async Task Get_after_playlist_deletion_invalidates_session()
    {
        var sut = CreateSut();
        HydrationMirrorsRefs();
        var playlistId = Guid.NewGuid();
        _files.GetShuffleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>())
            .Returns(Candidates(3, playlistId));

        var ct = TestContext.Current.CancellationToken;
        var created = await sut.CreateSessionAsync(_owner, Command(Guid.NewGuid(), playlistId), ct);

        _playlists.IsOwnerAsync(playlistId, _owner, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => sut.GetSessionBatchAsync(_owner, created.SessionId, 0, 50, ct);

        await act.Should().ThrowAsync<ShuffleSessionNotFoundException>();
        Func<Task> second = () => sut.GetSessionBatchAsync(_owner, created.SessionId, 0, 50, ct);
        await second.Should().ThrowAsync<ShuffleSessionNotFoundException>();
    }

    [Fact]
    public async Task Delete_missing_throws_and_delete_removes_session()
    {
        var sut = CreateSut();
        HydrationMirrorsRefs();
        _files.GetShuffleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>())
            .Returns(Candidates(3));
        _histories.GetShuffleListenRowsAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<ShuffleListenRow>());

        var ct = TestContext.Current.CancellationToken;
        Func<Task> missing = () => sut.DeleteSessionAsync(_owner, Guid.NewGuid(), ct);
        await missing.Should().ThrowAsync<ShuffleSessionNotFoundException>();

        var created = await sut.CreateSessionAsync(_owner, Command(Guid.NewGuid()), ct);
        await sut.DeleteSessionAsync(_owner, created.SessionId, ct);

        Func<Task> gone = () => sut.GetSessionBatchAsync(_owner, created.SessionId, 0, 50, ct);
        await gone.Should().ThrowAsync<ShuffleSessionNotFoundException>();
    }

    [Fact]
    public async Task Get_foreign_session_throws()
    {
        var sut = CreateSut();
        HydrationMirrorsRefs();
        _files.GetShuffleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(), Arg.Any<CancellationToken>())
            .Returns(Candidates(3));
        _histories.GetShuffleListenRowsAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<ShuffleListenRow>());

        var ct = TestContext.Current.CancellationToken;
        var created = await sut.CreateSessionAsync(_owner, Command(Guid.NewGuid()), ct);

        var act = () => sut.GetSessionBatchAsync(Guid.NewGuid(), created.SessionId, 0, 50, ct);

        await act.Should().ThrowAsync<ShuffleSessionNotFoundException>();
    }
}