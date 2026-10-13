using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Dto.Files.Streaming.Shuffle;
using Alexandria.Dto.Files.Streaming.Stats;
using Alexandria.Services.Streaming.Wrapped;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Streaming;

public class JumpBackInServiceTests
{
    private static readonly Guid Owner = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid FileA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FileB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime AsOf = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Cutoff = AsOf.AddDays(-30);

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IStreamHistoryRepository _histories = Substitute.For<IStreamHistoryRepository>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();

    public JumpBackInServiceTests()
    {
        _unitOfWork.StreamingHistories.Returns(_histories);
        _unitOfWork.Files.Returns(_files);
    }

    private ListeningStatsService CreateSut() => new(_unitOfWork);

    private static JumpBackInRankingRow Ranking(Guid fileId, int plays) => new()
    {
        FileId = fileId, QualifiedPlayCount = plays,
        TotalListenedSeconds = plays * 120L, LastPlayedAt = Cutoff.AddDays(-1)
    };

    [Fact]
    public async Task GetJumpBackInAsync_preserves_database_order_and_hydrates_only_bounded_ids_once()
    {
        var ct = TestContext.Current.CancellationToken;
        _histories.GetJumpBackInAsync(Owner, Cutoff, 2, ct).Returns([Ranking(FileB, 5), Ranking(FileA, 2)]);
        _files.GetPlaybackEntriesAsync(Owner, Arg.Any<PlaybackSourceDto>(),
                Arg.Any<IReadOnlyList<PlaybackSourceEntryRef>>(), ct)
            .Returns([new MediaFileDto { FileId = FileA }, new MediaFileDto { FileId = FileB }]);

        var result = await CreateSut().GetJumpBackInAsync(Owner, AsOf, 30, 2, ct);

        result.InactiveDays.Should().Be(30);
        result.InactiveBefore.Should().Be(Cutoff);
        result.Items.Select(s => s.File.FileId).Should().Equal(FileB, FileA);
        result.Items.Select(s => s.QualifiedPlayCount).Should().Equal(5, 2);
        result.Items.Select(s => s.TotalListenedSeconds).Should().Equal(600, 240);
        result.Items.Should().OnlyContain(s => s.LastPlayedAt == Cutoff.AddDays(-1));
        await _histories.Received(1).GetJumpBackInAsync(Owner, Cutoff, 2, ct);
        await _histories.DidNotReceive().GetListeningSessionRowsAsync(
            Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _histories.DidNotReceive().GetListeningHistoryRefsAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<DateTime?>());
        await _files.Received(1).GetPlaybackEntriesAsync(Owner,
            Arg.Is<PlaybackSourceDto>(s => !s.IsVideo && s.PlaylistId == null),
            Arg.Is<IReadOnlyList<PlaybackSourceEntryRef>>(refs => refs.Count == 2
                && refs[0].FileId == FileB && refs[1].FileId == FileA), ct);
    }

    [Fact]
    public async Task GetJumpBackInAsync_empty_ranking_does_not_load_playback_files()
    {
        var ct = TestContext.Current.CancellationToken;
        _histories.GetJumpBackInAsync(Owner, Cutoff, 6, ct).Returns([]);

        var result = await CreateSut().GetJumpBackInAsync(Owner, AsOf, 30, 6, ct);

        result.Items.Should().BeEmpty();
        await _files.DidNotReceive().GetPlaybackEntriesAsync(Arg.Any<Guid>(), Arg.Any<PlaybackSourceDto>(),
            Arg.Any<IReadOnlyList<PlaybackSourceEntryRef>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetJumpBackInAsync_drops_lost_files_without_refilling_and_keeps_fallback_playback_metadata()
    {
        var ct = TestContext.Current.CancellationToken;
        var fallback = new MediaFileDto
        {
            FileId = FileB, CurrentVersionId = Guid.NewGuid(), PlaybackVersionId = Guid.NewGuid(),
            TranspilationJobId = Guid.NewGuid(), Title = "Old favorite"
        };
        _histories.GetJumpBackInAsync(Owner, Cutoff, 2, ct).Returns([Ranking(FileA, 20), Ranking(FileB, 5)]);
        _files.GetPlaybackEntriesAsync(Owner, Arg.Any<PlaybackSourceDto>(),
            Arg.Any<IReadOnlyList<PlaybackSourceEntryRef>>(), ct).Returns([fallback]);

        var result = await CreateSut().GetJumpBackInAsync(Owner, AsOf, 30, 2, ct);

        result.Items.Should().ContainSingle().Which.File.Should().BeSameAs(fallback);
        fallback.PlaybackVersionId.Should().NotBe(fallback.CurrentVersionId);
        await _histories.Received(1).GetJumpBackInAsync(Owner, Cutoff, 2, ct);
    }
}
