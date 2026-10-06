using Alexandria.Common;
using Alexandria.Common.Exceptions.Streaming;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Services.Streaming;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Streaming;

public class StreamHistoryServiceTests
{
    private static readonly Guid Owner = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateTime PreviousPlay = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IStreamHistoryRepository _histories = Substitute.For<IStreamHistoryRepository>();
    private readonly IMediaMetadataRepository _metadata = Substitute.For<IMediaMetadataRepository>();
    private readonly StreamHistory _history = new()
    {
        Id = Guid.NewGuid(), UserId = Owner, FileId = Guid.NewGuid(),
        QualifiedPlayCount = 2, LastPlayedAt = PreviousPlay, TotalListenedSeconds = 1200
    };

    private readonly StreamSession _session = new() { Id = Guid.NewGuid() };

    private StreamHistoryService CreateSut(double duration)
    {
        _session.StreamHistoryId = _history.Id;
        _unitOfWork.StreamingHistories.Returns(_histories);
        _unitOfWork.MediaMetadata.Returns(_metadata);
        _histories.GetSessionByIdAsync(_session.Id, Arg.Any<CancellationToken>()).Returns(_session);
        _histories.GetByIdAndUserIdAsync(_history.Id, Owner, Arg.Any<CancellationToken>()).Returns(_history);
        _histories.CloseSessionAsync(_session, Owner, Arg.Any<CancellationToken>()).Returns(_history);
        _metadata.GetFileDurationAsync(_history.FileId, Arg.Any<CancellationToken>()).Returns(duration);

        return new StreamHistoryService(_unitOfWork, NullLogger<StreamHistoryService>.Instance);
    }

    [Theory]
    [InlineData(29, 600, false)]
    [InlineData(30, 600, true)]
    [InlineData(9, 20, false)]
    [InlineData(10, 20, true)]
    [InlineData(29, 0, false)]
    [InlineData(30, 0, true)]
    public async Task CloseSessionAsync_counts_qualified_listening_without_marking_playback_finished(
        long listened, double duration, bool qualified)
    {
        var sut = CreateSut(duration);

        var result = await sut.CloseSessionAsync(_session.Id,
            new CloseSessionRequest { EndPositionSeconds = listened, ListenedSeconds = listened },
            Owner, TestContext.Current.CancellationToken);

        _session.IsQualifiedPlay.Should().Be(qualified);
        _session.PlaybackFinished.Should().BeFalse();
        result.HasFinished.Should().BeFalse();

        await _histories.Received(1).CloseSessionAsync(_session, Owner, TestContext.Current.CancellationToken);
        await _histories.DidNotReceive().UpdateAsync(Arg.Any<StreamHistory>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(600, 1, false, false)]
    [InlineData(180, 30, true, false)]
    [InlineData(600, 0, true, false)]
    [InlineData(600, 10, true, true)]
    [InlineData(599, 600, true, true)]
    public async Task CloseSessionAsync_finishing_requires_an_end_report_and_a_valid_end_position(
        long position, long listened, bool reportedFinished, bool finished)
    {
        var sut = CreateSut(600);

        await sut.CloseSessionAsync(_session.Id,
            new CloseSessionRequest
            {
                EndPositionSeconds = position, ListenedSeconds = listened, PlaybackFinished = reportedFinished
            }, Owner, TestContext.Current.CancellationToken);

        _session.PlaybackFinished.Should().Be(finished);
        _session.IsQualifiedPlay.Should().Be(listened >= 30);

        await _histories.Received(1).CloseSessionAsync(_session, Owner, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CloseSessionAsync_does_not_clear_finished_status_when_a_replay_is_paused()
    {
        _history.HasFinished = true;
        var sut = CreateSut(600);

        var result = await sut.CloseSessionAsync(_session.Id,
            new CloseSessionRequest { EndPositionSeconds = 30, ListenedSeconds = 30 },
            Owner, TestContext.Current.CancellationToken);

        result.HasFinished.Should().BeTrue();
        _session.PlaybackFinished.Should().BeFalse();
    }

    [Fact]
    public async Task CloseSessionAsync_already_closed_does_not_count_the_play_twice()
    {
        _session.EndedAt = PreviousPlay;
        var sut = CreateSut(600);

        var result = await sut.CloseSessionAsync(_session.Id,
            new CloseSessionRequest { EndPositionSeconds = 600, ListenedSeconds = 600, PlaybackFinished = true },
            Owner, TestContext.Current.CancellationToken);

        result.QualifiedPlayCount.Should().Be(2);
        result.TotalListenedSeconds.Should().Be(1200);
        result.HasFinished.Should().BeFalse();
        await _histories.DidNotReceive().CloseSessionAsync(Arg.Any<StreamSession>(), Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _histories.DidNotReceive().UpdateAsync(Arg.Any<StreamHistory>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CloseSessionAsync_already_closed_still_checks_history_ownership()
    {
        _session.EndedAt = PreviousPlay;
        var sut = CreateSut(600);
        var otherUser = Guid.NewGuid();

        var close = () => sut.CloseSessionAsync(_session.Id,
            new CloseSessionRequest { EndPositionSeconds = 600, ListenedSeconds = 600 },
            otherUser, TestContext.Current.CancellationToken);

        await close.Should().ThrowAsync<StreamHistoryNotFoundException>();
        await _histories.DidNotReceive().CloseSessionAsync(Arg.Any<StreamSession>(), Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CloseSessionAsync_returns_the_repository_result_without_mutating_stale_history()
    {
        var sut = CreateSut(600);
        var persisted = new StreamHistory
        {
            Id = _history.Id, UserId = Owner, FileId = _history.FileId,
            QualifiedPlayCount = 5, TotalListenedSeconds = 1500, HasFinished = true,
        };

        _histories.CloseSessionAsync(_session, Owner, Arg.Any<CancellationToken>()).Returns(persisted);

        var result = await sut.CloseSessionAsync(_session.Id,
            new CloseSessionRequest { EndPositionSeconds = 30, ListenedSeconds = 30 },
            Owner, TestContext.Current.CancellationToken);

        result.TotalListenedSeconds.Should().Be(1500);
        result.QualifiedPlayCount.Should().Be(5);
        result.HasFinished.Should().BeTrue();
        _history.TotalListenedSeconds.Should().Be(1200);
        _history.QualifiedPlayCount.Should().Be(2);
    }

    [Fact]
    public async Task StartSessionAsync_existing_history_updates_position_without_rewriting_counters()
    {
        var sut = CreateSut(600);
        _histories.GetByUserAndFileAsync(Owner, _history.FileId, Arg.Any<CancellationToken>()).Returns(_history);
        _histories.CreateSessionAsync(Arg.Any<StreamSession>(), Arg.Any<CancellationToken>()).Returns(_session);

        var result = await sut.StartSessionAsync(
            new StartSessionRequest { FileId = _history.FileId, StartPositionSeconds = 10 },
            Owner, TestContext.Current.CancellationToken);

        result.Id.Should().Be(_session.Id);
        await _histories.Received(1).UpdatePositionAsync(_history.Id, Owner, 10, TestContext.Current.CancellationToken);
        await _histories.DidNotReceive().UpdateAsync(Arg.Any<StreamHistory>(), Arg.Any<CancellationToken>());
        _history.TotalListenedSeconds.Should().Be(1200);
        _history.QualifiedPlayCount.Should().Be(2);
    }
}
