using System.Text.Json;
using Alexandria.Common.Exceptions.Streaming;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Common.Summaries;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Stats;
using Alexandria.Dto.OverviewSummaries;
using Alexandria.Services.Streaming.Wrapped;
using AwesomeAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Streaming;

public class OverviewSummaryServiceTests
{
    private static readonly Guid Owner = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly SummaryPeriod Closed2024 = SummaryPeriod.ForYear(2024);

    private readonly IOverviewSummaryRepository _repository = Substitute.For<IOverviewSummaryRepository>();
    private readonly ISummaryGenerator _generator = Substitute.For<ISummaryGenerator>();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public OverviewSummaryServiceTests()
    {
        _generator.Kind.Returns(SummaryKind.Wrapped);
        _generator.SchemaVersion.Returns(3);
    }

    private OverviewSummaryService CreateSut() =>
        new(_repository, [_generator], TimeProvider.System, _cache, NullLogger<OverviewSummaryService>.Instance);

    private static WrappedPayload Payload() => new()
    {
        Summary = new WrappedSummaryFacts { Seconds = 3600, Sessions = 4 },
        Deck = new CuratedDeck([]),
        VisualIdentity = "abcdef1234567890",
        RecipeVersion = 2,
        CatalogVersion = "2026-09-13.1"
    };

    private static WrappedPayload PayloadWithCards() => Payload() with
    {
        Deck = new CuratedDeck([new WrappedCard(
            WrappedCardType.TopArtists,
            "What your year was built on",
            null,
            [],
            new CardVisualSeed("ember", "medium", "balanced", 0.2, 0, "aura"))])
    };

    private static OverviewSummary Stored(bool finalized) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Owner,
        Kind = SummaryKind.Wrapped,
        PeriodStart = Closed2024.Start,
        PeriodEnd = Closed2024.End,
        GeneratedAt = DateTime.UtcNow,
        FinalizedAt = finalized ? new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc) : null,
        SchemaVersion = 3,
        PayloadJson = SummaryJson.Serialize(PayloadWithCards())
    };

    [Fact]
    public async Task GetAsync_serves_frozen_row_without_regenerating()
    {
        _repository.GetByPeriodAsync(Owner, SummaryKind.Wrapped, Closed2024, Arg.Any<CancellationToken>())
            .Returns(Stored(finalized: true));

        var result = await CreateSut().GetAsync(Owner, SummaryKind.Wrapped, Closed2024, TestContext.Current.CancellationToken);

        result.IsFinal.Should().BeTrue();
        await _generator.DidNotReceive().GenerateAsync(Arg.Any<Guid>(), Arg.Any<SummaryPeriod>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<OverviewSummary>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_computes_live_without_writing()
    {
        _repository.GetByPeriodAsync(Owner, SummaryKind.Wrapped, Closed2024, Arg.Any<CancellationToken>())
            .Returns((OverviewSummary?)null);

        _generator.GenerateAsync(Owner, Closed2024, Arg.Any<CancellationToken>()).Returns(Payload());

        var result = await CreateSut().GetAsync(Owner, SummaryKind.Wrapped, Closed2024, TestContext.Current.CancellationToken);

        result.IsFinal.Should().BeFalse();
        result.Id.Should().Be(Guid.Empty);
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<OverviewSummary>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_serves_second_read_from_memory_cache()
    {
        _repository.GetByPeriodAsync(Owner, SummaryKind.Wrapped, Closed2024, Arg.Any<CancellationToken>())
            .Returns((OverviewSummary?)null);

        _generator.GenerateAsync(Owner, Closed2024, Arg.Any<CancellationToken>()).Returns(Payload());

        var sut = CreateSut();
        var ct = TestContext.Current.CancellationToken;

        await sut.GetAsync(Owner, SummaryKind.Wrapped, Closed2024, ct);
        await sut.GetAsync(Owner, SummaryKind.Wrapped, Closed2024, ct);

        await _generator.Received(1).GenerateAsync(Owner, Closed2024, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_ignores_legacy_provisional_row()
    {
        _repository.GetByPeriodAsync(Owner, SummaryKind.Wrapped, Closed2024, Arg.Any<CancellationToken>())
            .Returns(Stored(finalized: false));

        _generator.GenerateAsync(Owner, Closed2024, Arg.Any<CancellationToken>()).Returns(Payload());

        var result = await CreateSut().GetAsync(Owner, SummaryKind.Wrapped, Closed2024, TestContext.Current.CancellationToken);

        result.IsFinal.Should().BeFalse();
        await _generator.Received(1).GenerateAsync(Owner, Closed2024, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<OverviewSummary>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalizeAsync_throws_while_period_is_open()
    {
        var now = DateTime.UtcNow;

        var open = new SummaryPeriod(
            new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(now.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var act = () => CreateSut().FinalizeAsync(
            Owner, SummaryKind.Wrapped, open, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<SummaryPeriodNotClosedException>();
    }

    [Fact]
    public async Task FinalizeAsync_skips_storing_empty_deck()
    {
        _repository.GetByPeriodAsync(Owner, SummaryKind.Wrapped, Closed2024, Arg.Any<CancellationToken>())
            .Returns((OverviewSummary?)null);

        _generator.GenerateAsync(Owner, Closed2024, Arg.Any<CancellationToken>()).Returns(Payload());

        var result = await CreateSut().FinalizeAsync(Owner, SummaryKind.Wrapped, Closed2024, TestContext.Current.CancellationToken);

        result.IsFinal.Should().BeFalse();
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<OverviewSummary>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalizeAsync_evicts_cached_live_result()
    {
        var stored = Stored(finalized: true);

        _repository.GetByPeriodAsync(Owner, SummaryKind.Wrapped, Closed2024, Arg.Any<CancellationToken>())
            .Returns((OverviewSummary?)null, (OverviewSummary?)null, stored);

        _generator.GenerateAsync(Owner, Closed2024, Arg.Any<CancellationToken>()).Returns(PayloadWithCards());

        _repository.UpsertAsync(Arg.Any<OverviewSummary>(), Arg.Any<CancellationToken>())
            .Returns(UpsertOutcome.Inserted);

        var sut = CreateSut();
        var ct = TestContext.Current.CancellationToken;

        await sut.GetAsync(Owner, SummaryKind.Wrapped, Closed2024, ct);

        var finalized = await sut.FinalizeAsync(Owner, SummaryKind.Wrapped, Closed2024, ct);

        await sut.GetAsync(Owner, SummaryKind.Wrapped, Closed2024, ct);

        finalized.IsFinal.Should().BeTrue();
        await _generator.Received(2).GenerateAsync(Owner, Closed2024, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SummaryPeriod_rejects_non_utc_and_inverted_ranges()
    {
        var local = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Local);
        var utc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var notUtc = () => new SummaryPeriod(local, utc.AddYears(1));

        notUtc.Should().Throw<ArgumentException>();

        var inverted = () => new SummaryPeriod(utc, utc);

        inverted.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SummaryJson_keeps_enum_names_in_storage_and_numeric_values_on_the_wire()
    {
        var payload = PayloadWithCards();
        var storedJson = SummaryJson.Serialize(payload);

        using var stored = JsonDocument.Parse(storedJson);
        using var response = JsonDocument.Parse(JsonSerializer.Serialize<SummaryPayload>(
            payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var storedType = stored.RootElement.GetProperty("deck").GetProperty("cards")[0].GetProperty("type");
        var responseType = response.RootElement.GetProperty("deck").GetProperty("cards")[0].GetProperty("type");

        storedType.ValueKind.Should().Be(JsonValueKind.String);
        storedType.GetString().Should().Be(nameof(WrappedCardType.TopArtists));
        responseType.ValueKind.Should().Be(JsonValueKind.Number);
        responseType.GetInt32().Should().Be((int)WrappedCardType.TopArtists);
        SummaryJson.Deserialize(storedJson).Should().BeEquivalentTo(payload);
    }

    [Fact]
    public void SummaryJson_reads_discriminator_after_reordered_keys()
    {
        var json = """{"deck":{"cards":[]},"summary":{"seconds":60,"sessions":1,"tracks":0,"artists":0,"activeDays":1,"knownArtistShare":0,"hasPriorHistory":false,"historyFrom":null},"visualIdentity":"abcdef1234567890","recipeVersion":2,"catalogVersion":"2026-09-13.1","type":"wrapped"}""";

        var payload = SummaryJson.Deserialize(json);

        payload.Should().BeOfType<WrappedPayload>();
        ((WrappedPayload)payload).Summary.Seconds.Should().Be(60);
    }
}
