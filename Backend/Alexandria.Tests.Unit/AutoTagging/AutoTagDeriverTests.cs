using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Storage.AutoTagging;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.AutoTagging;

public class AutoTagDeriverTests
{
    private static readonly Guid RockId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BluesId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid NuMetalId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid PunkId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ChicagoBluesId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid AggressiveId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid HappyId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static readonly Dictionary<string, Tag> GenreTaxonomy = new()
    {
        ["Rock"] = Parent(RockId, "Rock"),
        ["Blues"] = Parent(BluesId, "Blues"),
        ["Rock---Nu Metal"] = Child(NuMetalId, "Rock---Nu Metal", "Nu Metal", RockId),
        ["Rock---Punk"] = Child(PunkId, "Rock---Punk", "Punk", RockId),
        ["Blues---Chicago Blues"] = Child(ChicagoBluesId, "Blues---Chicago Blues", "Chicago Blues", BluesId),
    };

    private static readonly Dictionary<string, Tag> MoodTaxonomy = new()
    {
        ["mood_aggressive:aggressive"] = Mood(AggressiveId, "Aggressive", "mood_aggressive:aggressive"),
        ["mood_happy:happy"] = Mood(HappyId, "Happy", "mood_happy:happy"),
    };

    // ---- Genre: top-N + threshold ----

    [Fact]
    public void DeriveGenre_keeps_top_three_above_threshold()
    {
        var payload = new EssentiaGenrePayload
        {
            Backbone = "effnet",
            Predictions =
            [
                new EssentiaGenrePrediction { Label = "Rock---Nu Metal", Score = 0.5 },
                new EssentiaGenrePrediction { Label = "Rock---Punk", Score = 0.3 },
                new EssentiaGenrePrediction { Label = "Blues---Chicago Blues", Score = 0.15 },
                new EssentiaGenrePrediction { Label = "Jazz---Bop", Score = 0.05 },
            ],
        };

        var result = AutoTagDeriver.DeriveGenre(payload, GenreTaxonomy, new GenreTaggingOptions());

        result.UnknownKeys.Should().BeEmpty();
        result.Candidates.Should().ContainSingle(c => c.TagId == NuMetalId && c.Confidence == 0.5);
        result.Candidates.Should().ContainSingle(c => c.TagId == PunkId && c.Confidence == 0.3);
        result.Candidates.Should().ContainSingle(c => c.TagId == ChicagoBluesId && c.Confidence == 0.15);
        // children (3) + parent rollups (Rock, Blues) = 5
        result.Candidates.Should().HaveCount(5);
    }

    [Fact]
    public void DeriveGenre_top_N_is_applied_before_threshold()
    {
        var payload = new EssentiaGenrePayload
        {
            Backbone = "effnet",
            Predictions =
            [
                new EssentiaGenrePrediction { Label = "Rock---Nu Metal", Score = 0.9 },
                new EssentiaGenrePrediction { Label = "Rock---Punk", Score = 0.8 },
                new EssentiaGenrePrediction { Label = "Blues---Chicago Blues", Score = 0.7 },
                new EssentiaGenrePrediction { Label = "Unknown---Fourth", Score = 0.6 },
            ],
        };

        var result = AutoTagDeriver.DeriveGenre(payload, GenreTaxonomy, new GenreTaggingOptions { TopN = 3 });

        result.Candidates.Should().Contain(c => c.TagId == NuMetalId);
        result.Candidates.Should().Contain(c => c.TagId == PunkId);
        result.Candidates.Should().Contain(c => c.TagId == ChicagoBluesId);
        // Fourth prediction falls outside top-3, so its (unknown) label never surfaces.
        result.Candidates.Should().HaveCount(5);
        result.UnknownKeys.Should().NotContain("Unknown---Fourth");
    }

    [Fact]
    public void DeriveGenre_below_threshold_is_dropped()
    {
        var payload = new EssentiaGenrePayload
        {
            Backbone = "effnet",
            Predictions = [new EssentiaGenrePrediction { Label = "Rock---Nu Metal", Score = 0.05 }],
        };

        var result = AutoTagDeriver.DeriveGenre(payload, GenreTaxonomy,
            new GenreTaggingOptions { ConfidenceThreshold = 0.1 });

        result.Candidates.Should().BeEmpty();
        result.UnknownKeys.Should().BeEmpty();
    }

    // ---- Genre: parent rollup ----

    [Fact]
    public void DeriveGenre_parent_rollup_uses_max_child_confidence()
    {
        var payload = new EssentiaGenrePayload
        {
            Backbone = "effnet",
            Predictions =
            [
                new EssentiaGenrePrediction { Label = "Rock---Nu Metal", Score = 0.6 },
                new EssentiaGenrePrediction { Label = "Rock---Punk", Score = 0.4 },
            ],
        };

        var result = AutoTagDeriver.DeriveGenre(payload, GenreTaxonomy, new GenreTaggingOptions());

        result.Candidates.Should().Contain(c => c.TagId == RockId && c.Confidence == 0.6);
        result.Candidates.Should().Contain(c => c.TagId == NuMetalId && c.Confidence == 0.6);
        result.Candidates.Should().Contain(c => c.TagId == PunkId && c.Confidence == 0.4);
        result.Candidates.Should().NotContain(c => c.TagId == BluesId);
    }

    // ---- Genre: drift ----

    [Fact]
    public void DeriveGenre_unknown_label_is_reported_and_not_candidate()
    {
        var payload = new EssentiaGenrePayload
        {
            Backbone = "effnet",
            Predictions = [new EssentiaGenrePrediction { Label = "Jazz---Bop", Score = 0.8 }],
        };

        var result = AutoTagDeriver.DeriveGenre(payload, GenreTaxonomy, new GenreTaggingOptions());

        result.Candidates.Should().BeEmpty();
        result.UnknownKeys.Should().ContainSingle("Jazz---Bop");
    }

    [Fact]
    public void DeriveGenre_unknown_label_does_not_hide_matched_labels()
    {
        var payload = new EssentiaGenrePayload
        {
            Backbone = "effnet",
            Predictions =
            [
                new EssentiaGenrePrediction { Label = "Rock---Nu Metal", Score = 0.6 },
                new EssentiaGenrePrediction { Label = "Jazz---Bop", Score = 0.5 },
            ],
        };

        var result = AutoTagDeriver.DeriveGenre(payload, GenreTaxonomy, new GenreTaggingOptions());

        result.Candidates.Should().Contain(c => c.TagId == NuMetalId);
        result.Candidates.Should().Contain(c => c.TagId == RockId);
        result.UnknownKeys.Should().ContainSingle("Jazz---Bop");
    }

    [Fact]
    public void DeriveGenre_empty_predictions_produce_nothing()
    {
        var result = AutoTagDeriver.DeriveGenre(
            new EssentiaGenrePayload { Backbone = "effnet" },
            GenreTaxonomy,
            new GenreTaggingOptions());

        result.Candidates.Should().BeEmpty();
        result.UnknownKeys.Should().BeEmpty();
    }

    // ---- Mood: winner + threshold ----

    [Fact]
    public void DeriveMood_winner_above_threshold_uses_composite_key()
    {
        var payload = new Dictionary<string, Dictionary<string, double>>
        {
            ["mood_aggressive"] = new() { ["not_aggressive"] = 0.3, ["aggressive"] = 0.7 },
        };

        var result = AutoTagDeriver.DeriveMood(payload, MoodTaxonomy, new MoodTaggingOptions());

        result.Candidates.Should().ContainSingle(c => c.TagId == AggressiveId && c.Confidence == 0.7);
        result.UnknownKeys.Should().BeEmpty();
    }

    [Fact]
    public void DeriveMood_winner_below_threshold_tags_neither_pole()
    {
        var payload = new Dictionary<string, Dictionary<string, double>>
        {
            ["mood_aggressive"] = new() { ["not_aggressive"] = 0.55, ["aggressive"] = 0.45 },
        };

        var result = AutoTagDeriver.DeriveMood(payload, MoodTaxonomy, new MoodTaggingOptions());

        result.Candidates.Should().BeEmpty();
        result.UnknownKeys.Should().BeEmpty();
    }

    [Fact]
    public void DeriveMood_near_5050_split_tags_neither_pole()
    {
        var payload = new Dictionary<string, Dictionary<string, double>>
        {
            ["mood_happy"] = new() { ["non_happy"] = 0.51, ["happy"] = 0.49 },
        };

        var result = AutoTagDeriver.DeriveMood(payload, MoodTaxonomy, new MoodTaggingOptions());

        result.Candidates.Should().BeEmpty();
        result.UnknownKeys.Should().BeEmpty();
    }

    [Fact]
    public void DeriveMood_positive_pole_below_threshold_is_not_reported_as_drift()
    {
        // Winner pole has no taxonomy row (negative gradient) but also fails threshold:
        // skipped quietly, not logged as drift.
        var payload = new Dictionary<string, Dictionary<string, double>>
        {
            ["mood_aggressive"] = new() { ["not_aggressive"] = 0.6, ["aggressive"] = 0.4 },
        };

        var result = AutoTagDeriver.DeriveMood(payload, MoodTaxonomy, new MoodTaggingOptions());

        result.Candidates.Should().BeEmpty();
        result.UnknownKeys.Should().BeEmpty();
    }

    [Fact]
    public void DeriveMood_unknown_axis_is_reported_as_drift()
    {
        var payload = new Dictionary<string, Dictionary<string, double>>
        {
            ["mood_aggressive"] = new() { ["aggressive"] = 0.9, ["not_aggressive"] = 0.1 },
            ["mood_exotic"] = new() { ["exotic"] = 0.8, ["non_exotic"] = 0.2 },
        };

        var result = AutoTagDeriver.DeriveMood(payload, MoodTaxonomy, new MoodTaggingOptions());

        result.Candidates.Should().ContainSingle(c => c.TagId == AggressiveId);
        result.UnknownKeys.Should().ContainSingle("mood_exotic:exotic");
    }

    [Fact]
    public void DeriveMood_negative_pole_without_tag_row_is_reported_as_drift()
    {
        var payload = new Dictionary<string, Dictionary<string, double>>
        {
            ["mood_aggressive"] = new() { ["not_aggressive"] = 0.9, ["aggressive"] = 0.1 },
        };

        var result = AutoTagDeriver.DeriveMood(payload, MoodTaxonomy, new MoodTaggingOptions());

        result.Candidates.Should().BeEmpty();
        result.UnknownKeys.Should().ContainSingle("mood_aggressive:not_aggressive");
    }

    private static Tag Parent(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        Icon = "tag",
        Color = "#000000",
        ExternalKey = name,
        Facet = TagFacet.Genre,
    };

    private static Tag Child(Guid id, string externalKey, string name, Guid parentId) => new()
    {
        Id = id,
        Name = name,
        Icon = "tag",
        Color = "#000000",
        ExternalKey = externalKey,
        Facet = TagFacet.Genre,
        ParentId = parentId,
    };

    private static Tag Mood(Guid id, string name, string externalKey) => new()
    {
        Id = id,
        Name = name,
        Icon = "heart",
        Color = "#000000",
        ExternalKey = externalKey,
        Facet = TagFacet.Mood,
    };
}