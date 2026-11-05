using System.Text.Json;
using Alexandria.Data.Models;
using Alexandria.Data.Seeders;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Tags;

public class TagDescriptionsResourceTests
{
    private const string TaxonomyResource = "Alexandria.Data.Seeders.Tagging.tagging-taxonomy.json";
    private const string DescriptionsResource = "Alexandria.Data.Seeders.Tagging.tag-descriptions.json";

    [Fact]
    public void Every_taxonomy_key_has_a_description_and_no_extras_exist()
    {
        var taxonomyKeys = LoadTaxonomyKeys();
        var descriptions = LoadDescriptions();

        taxonomyKeys.Should().HaveCount(542);
        descriptions.Keys.Should().BeEquivalentTo(taxonomyKeys);
    }

    [Fact]
    public void Descriptions_are_readable_and_fit_the_column()
    {
        var descriptions = LoadDescriptions();

        foreach (var (key, description) in descriptions)
        {
            description.Should().NotBeNullOrWhiteSpace($"'{key}' needs a description");
            description.Length.Should().BeLessThanOrEqualTo(
                ValidationConstants.StringLengths.MediumString, $"'{key}' must fit Tag.Description");

            // Guards against regressing to the old generated wording.
            description.Should().NotContain("subgenre:", $"'{key}' uses the old template");
            description.Should().NotStartWith("Top-level", $"'{key}' uses the old template");
            description.Should().NotContain("classifier", $"'{key}' leaks classifier wording");
        }
    }

    private static List<string> LoadTaxonomyKeys()
    {
        using var document = JsonDocument.Parse(ReadResource(TaxonomyResource));
        var root = document.RootElement;

        var parents = root.GetProperty("genreParents").EnumerateArray().Select(e => e.GetString()!);
        var labels = root.GetProperty("genreLabels").EnumerateArray().Select(e => e.GetString()!);
        var moods = root.GetProperty("moodTags").EnumerateArray()
            .Select(e => e.GetProperty("externalKey").GetString()!);

        return [.. parents, .. labels, .. moods];
    }

    private static Dictionary<string, string> LoadDescriptions()
    {
        using var document = JsonDocument.Parse(ReadResource(DescriptionsResource));

        return document.RootElement.GetProperty("descriptions").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString()!);
    }

    private static string ReadResource(string name)
    {
        using var stream = typeof(TagSeeder).Assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"Embedded resource '{name}' not found.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
