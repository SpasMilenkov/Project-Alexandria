using System.Text.Json;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Streaming.Wrapped;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class ListeningStoryTests
{
    [Theory]
    [InlineData(119.99, WrappedDurationTier.UnderTwoHours)]
    [InlineData(120, WrappedDurationTier.TwoToThree)]
    [InlineData(180, WrappedDurationTier.ThreeToSix)]
    [InlineData(360, WrappedDurationTier.SixToTwelve)]
    [InlineData(720, WrappedDurationTier.TwelveToTwenty)]
    [InlineData(1200, WrappedDurationTier.TwentyToForty)]
    [InlineData(2400, WrappedDurationTier.FortyToEighty)]
    [InlineData(4800, WrappedDurationTier.EightyToOneTwenty)]
    [InlineData(7200, WrappedDurationTier.OneTwentyPlus)]
    public void TierFor_boundary_uses_nonoverlapping_bucket(double minutes, WrappedDurationTier expected)
        => ListeningComparisons.TierFor(minutes).Should().Be(expected);

    [Fact]
    public void Select_packaged_catalog_is_valid_and_canonicalizes_duplicate()
    {
        ListeningComparisons.CatalogCount.Should().Be(115);
        ListeningComparisons.Select(119 * 60, "user").Should().BeEmpty();

        var comparisons = ListeningComparisons.Select(80 * 3600, "user");
        var comparison = comparisons.Single(c => c.Key == "two_workweeks");

        comparison.Key.Should().Be("two_workweeks");
        comparison.Tier.Should().Be(WrappedDurationTier.EightyToOneTwenty);
        comparisons.Should().NotContain(c => c.Key == "ten_workdays");
    }

    [Fact]
    public void Select_same_identity_preserves_choices_and_numeric_json_contract()
    {
        var first = ListeningComparisons.Select(120 * 60, "user");

        first.Should().Equal(ListeningComparisons.Select(120 * 60, "user"));
        first[0].Relationship.Should().Be(WrappedDurationRelationship.Approximately);
        first[0].Name.Should().NotContain("world record");
        first[0].Copy.Should().StartWith("About as much time as");
        first[0].Description.Should().Be(DurationCatalogLoader.Load().Single(a => a.Id == first[0].Key).Copy);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(first[0], new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        json.RootElement.GetProperty("category").ValueKind.Should().Be(JsonValueKind.Number);
        json.RootElement.GetProperty("relationship").ValueKind.Should().Be(JsonValueKind.Number);
        json.RootElement.GetProperty("durationMinutes").GetDouble().Should().BeGreaterThan(120);
        json.RootElement.GetProperty("description").GetString().Should().Be(first[0].Description);
    }

    [Fact]
    public void Select_wide_catalog_gap_and_beyond_catalog_use_completed_milestones()
    {
        var gap = ListeningComparisons.Select(250 * 3600, "user").Single();

        gap.Key.Should().Be("voyager_nonstop_1986");
        gap.Relationship.Should().Be(WrappedDurationRelationship.MilestonePassed);

        var beyond = ListeningComparisons.Select(10000L * 3600, "user").Single();

        beyond.Key.Should().Be("one_continuous_year");
        beyond.Relationship.Should().Be(WrappedDurationRelationship.MilestonePassed);
        beyond.DurationLabel.Should().Be("8,760h");
        beyond.Description.Should().Be("A full year without a moment of silence.");
    }

    [Theory]
    [InlineData("animation", WrappedDurationCategory.Animation, 9)]
    [InlineData("theatre", WrappedDurationCategory.Theatre, 10)]
    [InlineData("audiobook", WrappedDurationCategory.Audiobook, 11)]
    public void CategoryFor_new_categories_append_numeric_contract_values(
        string key, WrappedDurationCategory category, int ordinal)
    {
        DurationCatalogLoader.CategoryFor(key).Should().Be(category);
        ((int)category).Should().Be(ordinal);

        var json = JsonSerializer.Serialize(category, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Be(ordinal.ToString());
    }

    [Theory]
    [InlineData(125, WrappedDurationCategory.Animation)]
    [InlineData(315, WrappedDurationCategory.Theatre)]
    [InlineData(498, WrappedDurationCategory.Audiobook)]
    public void Select_new_categories_include_authored_reference_descriptions(
        int minutes, WrappedDurationCategory category)
    {
        var catalog = DurationCatalogLoader.Load().ToDictionary(a => a.Id);
        var comparisons = ListeningComparisons.Select(minutes * 60L, "user");

        comparisons.Should().Contain(c => c.Category == category);

        foreach (var comparison in comparisons)
            comparison.Description.Should().Be(catalog[comparison.Key].Copy);
    }

    [Theory]
    [InlineData(0, WrappedTimeScene.Night)]
    [InlineData(5, WrappedTimeScene.Night)]
    [InlineData(6, WrappedTimeScene.Morning)]
    [InlineData(10, WrappedTimeScene.Morning)]
    [InlineData(11, WrappedTimeScene.Midday)]
    [InlineData(13, WrappedTimeScene.Midday)]
    [InlineData(14, WrappedTimeScene.Afternoon)]
    [InlineData(17, WrappedTimeScene.Afternoon)]
    [InlineData(18, WrappedTimeScene.Evening)]
    [InlineData(23, WrappedTimeScene.Evening)]
    public void SceneForHour_boundary_preserves_five_distinct_windows(int hour, WrappedTimeScene expected)
        => ListeningRhythm.SceneForHour(hour).Should().Be(expected);
}
