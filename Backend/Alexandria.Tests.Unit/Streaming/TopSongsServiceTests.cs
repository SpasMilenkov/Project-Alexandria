using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Dto.Files.Streaming.Shuffle;
using Alexandria.Dto.Files.Streaming.Stats;
using Alexandria.Services.Streaming.Wrapped;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Streaming;

public class TopSongsServiceTests
{
    private static readonly Guid Owner = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid FileA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FileB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid FileC = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime From = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = From.AddDays(7);

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IStreamHistoryRepository _histories = Substitute.For<IStreamHistoryRepository>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();

    public TopSongsServiceTests()
    {
        _unitOfWork.StreamingHistories.Returns(_histories);
        _unitOfWork.Files.Returns(_files);
    }

    private ListeningStatsService CreateSut() => new(_unitOfWork);

    private static TopSongRankingRow Ranking(Guid fileId, int plays, long seconds) =>
        new() { FileId = fileId, QualifiedPlayCount = plays, TotalListenedSeconds = seconds };

    private void ArrangeRankings(int limit, params TopSongRankingRow[] rows)
    {
        _histories.GetTopSongsAsync(Owner, From, To, limit, TestContext.Current.CancellationToken)
            .Returns(rows);
    }

    private void ArrangePlayable(params MediaFileDto[] files)
    {
        _files.GetPlaybackEntriesAsync(Owner,
                Arg.Is<PlaybackSourceDto>(s => !s.IsVideo && s.PlaylistId == null),
                Arg.Any<IReadOnlyList<PlaybackSourceEntryRef>>(), TestContext.Current.CancellationToken)
            .Returns(files);
    }

    [Fact]
    public async Task GetTopSongsAsync_preserves_repository_ranking_when_hydration_returns_another_order()
    {
        ArrangeRankings(3, Ranking(FileB, 2, 30), Ranking(FileA, 2, 20), Ranking(FileC, 1, 1000));
        ArrangePlayable(new MediaFileDto { FileId = FileC }, new MediaFileDto { FileId = FileA },
            new MediaFileDto { FileId = FileB });

        var result = await CreateSut().GetTopSongsAsync(Owner, From, To, 3, TestContext.Current.CancellationToken);

        result.From.Should().Be(From);
        result.To.Should().Be(To);
        result.Items.Select(s => s.File.FileId).Should().Equal(FileB, FileA, FileC);
        result.Items.Select(s => s.QualifiedPlayCount).Should().Equal(2, 2, 1);
        result.Items.Select(s => s.TotalListenedSeconds).Should().Equal(30, 20, 1000);
    }

    [Fact]
    public async Task GetTopSongsAsync_fetches_only_bounded_rankings_and_hydrates_them_once()
    {
        ArrangeRankings(2, Ranking(FileA, 1, 603), Ranking(FileC, 1, 500));
        ArrangePlayable(new MediaFileDto { FileId = FileA }, new MediaFileDto { FileId = FileC });

        await CreateSut().GetTopSongsAsync(Owner, From, To, 2, TestContext.Current.CancellationToken);

        await _histories.Received(1).GetTopSongsAsync(Owner, From, To, 2,
            TestContext.Current.CancellationToken);
        await _histories.DidNotReceive().GetListeningSessionRowsAsync(
            Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _histories.DidNotReceive().GetListeningHistoryRefsAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<DateTime?>());
        await _files.Received(1).GetPlaybackEntriesAsync(Owner, Arg.Any<PlaybackSourceDto>(),
            Arg.Is<IReadOnlyList<PlaybackSourceEntryRef>>(refs => refs.Count == 2
                && refs[0].FileId == FileA && refs[1].FileId == FileC), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetTopSongsAsync_omits_files_lost_after_ranking_and_preserves_playback_metadata()
    {
        var playbackVersion = Guid.NewGuid();
        var playable = new MediaFileDto
        {
            FileId = FileB,
            CurrentVersionId = Guid.NewGuid(),
            PlaybackVersionId = playbackVersion,
            TranspilationJobId = Guid.NewGuid(),
            Title = "Fallback-version song",
            Artist = "Artist"
        };
        ArrangeRankings(2, Ranking(FileA, 2, 100), Ranking(FileB, 1, 50));
        ArrangePlayable(playable);

        var result = await CreateSut().GetTopSongsAsync(Owner, From, To, 2, TestContext.Current.CancellationToken);

        result.Items.Should().ContainSingle().Which.File.Should().BeSameAs(playable);
        result.Items[0].File.PlaybackVersionId.Should().Be(playbackVersion);
    }

    [Fact]
    public async Task GetTopSongsAsync_no_ranked_files_returns_empty_without_hydrating()
    {
        ArrangeRankings(5);

        var result = await CreateSut().GetTopSongsAsync(Owner, From, To, 5, TestContext.Current.CancellationToken);

        result.Items.Should().BeEmpty();
        await _files.DidNotReceive().GetPlaybackEntriesAsync(Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(),
            Arg.Any<IReadOnlyList<PlaybackSourceEntryRef>>(), Arg.Any<CancellationToken>());
    }
}
