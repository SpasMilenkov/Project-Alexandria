using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Summaries;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Stats;
using Alexandria.Dto.OverviewSummaries;
using Alexandria.Services.Streaming.Wrapped;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Streaming;

public class ListeningStatsServiceTests
{
    private static readonly Guid Owner = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid FileA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime From = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IStreamHistoryRepository _histories = Substitute.For<IStreamHistoryRepository>();

    public ListeningStatsServiceTests()
    {
        _unitOfWork.StreamingHistories.Returns(_histories);
    }

    private ListeningStatsService CreateSut() => new(_unitOfWork);

    [Fact]
    public async Task GetWrappedAsync_returns_deck_for_user_range()
    {
        _histories.GetListeningSessionRowsAsync(Owner, From, To, Arg.Any<CancellationToken>())
            .Returns(new List<ListeningSessionRow>
            {
                new(Guid.NewGuid(), FileA, "drift.mp3", "Drift", "arcane",
                    new DateTime(2026, 3, 1, 2, 0, 0, DateTimeKind.Utc), 600, true),
            });

        _histories.GetListeningHistoryRefsAsync(Owner, Arg.Any<CancellationToken>(), From)
            .Returns(new List<ListeningHistoryRef>());

        var result = await CreateSut().GetWrappedAsync(Owner, From, To, TestContext.Current.CancellationToken);

        result.From.Should().Be(From);
        result.To.Should().Be(To);

        result.Deck.Cards.Select(c => c.Type).Should().Contain(
            [WrappedCardType.TopArtists, WrappedCardType.TopSongs, WrappedCardType.ListeningTime]);

        result.Deck.Cards.First(c => c.Type == WrappedCardType.Persona)
            .Headline.Should().Be("Your rhythm is taking shape");
    }

    [Fact]
    public async Task GetWrappedAsync_empty_library_returns_empty_deck()
    {
        _histories.GetListeningSessionRowsAsync(Owner, From, To, Arg.Any<CancellationToken>())
            .Returns(new List<ListeningSessionRow>());

        _histories.GetListeningHistoryRefsAsync(Owner, Arg.Any<CancellationToken>(), From)
            .Returns(new List<ListeningHistoryRef>());

        var result = await CreateSut().GetWrappedAsync(Owner, From, To, TestContext.Current.CancellationToken);

        result.Deck.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTimelineAsync_returns_user_range_and_forwards_cancellation()
    {
        var ct = TestContext.Current.CancellationToken;

        _histories.GetListeningSessionRowsAsync(Owner, From, To, ct)
            .Returns(new List<ListeningSessionRow>
            {
                new(Guid.NewGuid(), FileA, "drift.mp3", "Drift", "arcane",
                    new DateTime(2026, 3, 1, 2, 0, 0, DateTimeKind.Utc), 600, true)
            });

        var result = await CreateSut().GetTimelineAsync(Owner, From, To, ct);

        result.From.Should().Be(From);
        result.To.Should().Be(To);
        result.TotalSeconds.Should().Be(600);
        result.SessionCount.Should().Be(1);
        result.QualifiedPlayCount.Should().Be(1);
        result.ActiveDays.Should().Be(1);
        result.Days.Should().ContainSingle().Which.Date.Should().Be("2026-03-01");
        await _histories.Received(1).GetListeningSessionRowsAsync(Owner, From, To, ct);

        await _histories.DidNotReceive().GetListeningHistoryRefsAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<DateTime?>());
    }

    [Fact]
    public async Task GetWrappedAsync_matches_persisted_payload_and_keeps_stable_identity()
    {
        var ct = TestContext.Current.CancellationToken;

        _histories.GetListeningSessionRowsAsync(Owner, From, To, ct)
            .Returns(new List<ListeningSessionRow>
            {
                new(Guid.NewGuid(), FileA, "drift.mp3", "Drift", "arcane",
                    new DateTime(2026, 3, 1, 2, 0, 0, DateTimeKind.Utc), 7200, true, 300),
                new(Guid.NewGuid(), FileA, "drift.mp3", "Drift", "arcane",
                    new DateTime(2026, 3, 1, 3, 0, 0, DateTimeKind.Utc), 20, false, 300)
            });

        _histories.GetListeningHistoryRefsAsync(Owner, ct, From)
            .Returns(new List<ListeningHistoryRef>
            {
                new(FileA, "arcane", From.AddDays(-1))
            });

        var service = CreateSut();
        var first = await service.GetWrappedAsync(Owner, From, To, ct);
        var second = await service.GetWrappedAsync(Owner, From, To, ct);
        var generator = new WrappedSummaryGenerator(_unitOfWork);
        var payload = (WrappedPayload)await generator.GenerateAsync(Owner, new SummaryPeriod(From, To), ct);

        first.VisualIdentity.Should().HaveLength(16);
        second.VisualIdentity.Should().Be(first.VisualIdentity);
        second.Deck.Should().BeEquivalentTo(first.Deck);
        payload.VisualIdentity.Should().Be(first.VisualIdentity);
        payload.Deck.Should().BeEquivalentTo(first.Deck);
        payload.Summary.Should().BeEquivalentTo(first.Summary);
        first.Summary.Seconds.Should().Be(7220);
        first.Summary.Sessions.Should().Be(2);
        first.Summary.QualifiedPlayCount.Should().Be(1);
        first.Summary.Tracks.Should().Be(1);
        first.Summary.Artists.Should().Be(1);
        first.Summary.ActiveDays.Should().Be(1);
        first.Summary.KnownArtistShare.Should().Be(1);
        first.Summary.HasPriorHistory.Should().BeTrue();
        first.Summary.HistoryFrom.Should().Be(From.AddDays(-1));
        generator.SchemaVersion.Should().Be(3);
        payload.RecipeVersion.Should().Be(2);
        payload.CatalogVersion.Should().Be("2026-10-06.1");
        await _histories.Received(3).GetListeningSessionRowsAsync(Owner, From, To, ct);
        await _histories.Received(3).GetListeningHistoryRefsAsync(Owner, ct, From);
    }
}
