using System.Text.Json.Serialization;
using Alexandria.Dto.Files.Streaming.Stats;

namespace Alexandria.Dto.OverviewSummaries;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(WrappedPayload), "wrapped")]
public abstract record SummaryPayload;

public sealed record WrappedPayload : SummaryPayload
{
    public required WrappedSummaryFacts Summary { get; init; }
    public required CuratedDeck Deck { get; init; }
    public required string VisualIdentity { get; init; }
    public required int RecipeVersion { get; init; }
    public required string CatalogVersion { get; init; }
}
