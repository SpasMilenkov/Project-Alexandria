using System.Globalization;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files.Streaming.Stats;

/// <summary>
/// Render inputs for one card. Color/density feed the existing art generator's
/// established slots (persona bucket as color seed, skip rate as density);
/// shape is a fixed per-card-type grammar so replayed-vs-duration stay legible
/// as different facts. FillLevel only matters for the fill grammar.
/// </summary>
public sealed record CardVisualSeed(
    string ColorKey,
    string Saturation,
    string Contrast,
    double Density,
    double FillLevel,
    string ShapeGrammar);

/// <summary>One ranked row inside a card (countdown entries, bookend pair, new artists).</summary>
public sealed record WrappedCardEntry(
    int Rank,
    string Title,
    string? Subtitle,
    string? Date = null,
    string? EntityId = null,
    string? Artist = null,
    long Seconds = 0,
    int Plays = 0,
    double Share = 0);

public sealed record WrappedComparison(
    string Key,
    string Name,
    WrappedDurationCategory Category,
    WrappedDurationTier Tier,
    double DurationMinutes,
    IReadOnlyList<double>? DurationRangeMinutes,
    string DurationLabel,
    bool Exact,
    WrappedDurationRelationship Relationship,
    string Copy,
    string CatalogVersion,
    string? Description = null);

public sealed record WrappedRhythm(
    WrappedTimeScene? Scene,
    WrappedRhythmEvidence Evidence,
    double Share,
    IReadOnlyList<long> WindowSeconds);

/// <summary>Typed facts, never parsed back out of display copy. Series units are listening seconds.</summary>
public sealed record WrappedCardFacts
{
    public long Seconds { get; init; }
    public int Count { get; init; }
    public double Share { get; init; }
    public double BaselineSeconds { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public string? Detail { get; init; }
    public IReadOnlyList<long> Weights { get; init; } = [];
    public IReadOnlyList<ListeningPeriodPoint> Series { get; init; } = [];
    public WrappedComparison? Comparison { get; init; }
    public IReadOnlyList<WrappedComparison> ComparisonAlternatives { get; init; } = [];
    public WrappedRhythm? Rhythm { get; init; }
}

/// <summary>
/// One curated card: framed copy plus its visual seed. Facts live in the copy;
/// structured flashes for render live in <see cref="Entries"/> and
/// <see cref="CardVisualSeed"/>.
/// </summary>
public sealed record WrappedCard(
    WrappedCardType Type,
    string Headline,
    string? Subline,
    IReadOnlyList<WrappedCardEntry> Entries,
    CardVisualSeed Visual)
{
    public string Id { get; init; } = ((int)Type).ToString(CultureInfo.InvariantCulture);
    public WrappedCardFacts Facts { get; init; } = new();
}

/// <summary>Curate-layer output: the selected, framed deck. What the Wrapped view renders.</summary>
public sealed record CuratedDeck(IReadOnlyList<WrappedCard> Cards);
