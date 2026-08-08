using System.Text.Json;
using System.Text.Json.Serialization;
using Alexandria.Common.Config;
using Alexandria.Common.Seeding;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Alexandria.Data.Seeders;

/// <summary>
/// Seeds the auto-tagging taxonomy, owned by the system account, exactly once and
/// idempotently on <see cref="Tag.ExternalKey"/>. Source of truth is the embedded
/// <c>Seeders/Tagging/tagging-taxonomy.json</c> resource, generated from the MAEST
/// model metadata (<c>discogs-maest-30s-pw-519l-2</c>) plus the mood composite
/// <c>{axis}:{pole}</c> keys. Expected seed: 15 parents + 519 genre children +
/// 8 mood tags = 542 rows.
///
/// Conventions (presentation, not taxonomy):
///   - Genre parents: unique Icon per parent (see <see cref="GenreParentIcons"/>) and a
///     unique Color per parent (see <see cref="GenrePalette"/>), both by parent order.
///   - Genre children: inherit the parent's Icon/Color; <c>Name</c> is the child part of
///     the discogs label (after the first <c>---</c>); <c>ExternalKey</c> is the full label;
///     <c>ParentId</c> points at the declared parent tag.
///   - Mood tags: Icon/Color from <see cref="MoodPresentation"/>, keyed by <c>ExternalKey</c>.
///   - All rows: <c>Facet</c> = <see cref="TagFacet.Genre"/>/<see cref="TagFacet.Mood"/>,
///     <c>OwnerId</c> and <c>UpdatedBy</c> = <see cref="SystemConfig.SystemId"/>.
/// </summary>
public class TagSeeder(
    AlexandriaDbContext dbContext,
    ILogger<TagSeeder> logger) : ISeeder
{
    private const string TaxonomyResource = "Alexandria.Data.Seeders.Tagging.tagging-taxonomy.json";

    private static readonly string[] GenrePalette =
    [
        "#EF4444", "#F97316", "#F59E0B", "#EAB308", "#84CC16",
        "#22C55E", "#10B981", "#14B8A6", "#0EA5E9", "#3B82F6",
        "#6366F1", "#8B5CF6", "#A855F7", "#D946EF", "#EC4899",
    ];

    // One unique icon per genre parent (aligned with GenrePalette by index)
    private static readonly string[] GenreParentIcons =
    [
        "heart", "flag", "star", "document", "settings",
        "location", "group", "message", "clock", "bell",
        "folder", "tag", "link", "alert", "video",
    ];

    private static readonly Dictionary<string, (string Icon, string Color)> MoodPresentation = new()
    {
        ["mood_aggressive:aggressive"] = ("alert", "#EF4444"),
        ["mood_happy:happy"] = ("heart", "#F59E0B"),
        ["mood_party:party"] = ("star", "#8B5CF6"),
        ["mood_relaxed:relaxed"] = ("check", "#10B981"),
        ["mood_sad:sad"] = ("flag", "#3B82F6"),
        ["mood_acoustic:acoustic"] = ("bookmark", "#0EA5E9"),
        ["voice_instrumental:instrumental"] = ("settings", "#EC4899"),
        ["voice_instrumental:voice"] = ("user", "#78716C"),
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var taxonomy = await LoadTaxonomyAsync(ct);

        var existingKeys = await dbContext.Tags
            .Where(t => t.ExternalKey != null)
            .Select(t => t.ExternalKey!)
            .ToHashSetAsync(ct);

        var parentsToCreate = CreateGenreParents(taxonomy, existingKeys);
        dbContext.Tags.AddRange(parentsToCreate);
        await dbContext.SaveChangesAsync(ct);

        var parentTags = await dbContext.Tags
            .Where(t => t.Facet == TagFacet.Genre && t.ParentId == null)
            .ToDictionaryAsync(t => t.Name, t => t, ct);

        var childrenToCreate = CreateGenreChildren(taxonomy, parentTags, existingKeys);
        dbContext.Tags.AddRange(childrenToCreate);
        await dbContext.SaveChangesAsync(ct);

        var moodsToCreate = CreateMoodTags(taxonomy, existingKeys);
        dbContext.Tags.AddRange(moodsToCreate);
        await dbContext.SaveChangesAsync(ct);

        var created = parentsToCreate.Count + childrenToCreate.Count + moodsToCreate.Count;
        logger.LogInformation(
            "Tag taxonomy seed complete: {Created} created, {Existing} already present.",
            created, existingKeys.Count);
    }

    private static List<Tag> CreateGenreParents(TaggingTaxonomy taxonomy, HashSet<string> existingKeys)
    {
        var tags = new List<Tag>();
        for (var i = 0; i < taxonomy.GenreParents.Count; i++)
        {
            var name = taxonomy.GenreParents[i];
            if (existingKeys.Contains(name)) continue;

            tags.Add(new Tag
            {
                Name = name,
                Icon = GenreParentIcons[i % GenreParentIcons.Length],
                Color = GenrePalette[i % GenrePalette.Length],
                Description = $"Top-level {name} genre.",
                ExternalKey = name,
                Facet = TagFacet.Genre,
                OwnerId = SystemConfig.SystemId,
                UpdatedBy = SystemConfig.SystemId,
            });
        }

        return tags;
    }

    private static List<Tag> CreateGenreChildren(
        TaggingTaxonomy taxonomy,
        Dictionary<string, Tag> parentTags,
        HashSet<string> existingKeys)
    {
        var tags = new List<Tag>();
        foreach (var label in taxonomy.GenreLabels)
        {
            if (existingKeys.Contains(label)) continue;

            var parentName = label.Split("---", 2)[0];
            var childName = label.Split("---", 2)[1];

            if (!taxonomy.GenreParents.Contains(parentName))
            {
                throw new InvalidOperationException(
                    $"Taxonomy label '{label}' references undeclared parent '{parentName}'.");
            }

            if (!parentTags.TryGetValue(parentName, out var parent))
            {
                throw new InvalidOperationException(
                    $"Taxonomy label '{label}' has no seeded parent tag '{parentName}'.");
            }

            tags.Add(new Tag
            {
                Name = childName,
                Icon = parent.Icon,
                Color = parent.Color,
                Description = $"{parentName} subgenre: {childName}.",
                ExternalKey = label,
                Facet = TagFacet.Genre,
                ParentId = parent.Id,
                OwnerId = SystemConfig.SystemId,
                UpdatedBy = SystemConfig.SystemId,
            });
        }

        return tags;
    }

    private static List<Tag> CreateMoodTags(TaggingTaxonomy taxonomy, HashSet<string> existingKeys)
    {
        var tags = new List<Tag>();
        foreach (var entry in taxonomy.MoodTags)
        {
            if (existingKeys.Contains(entry.ExternalKey)) continue;

            var presentation = MoodPresentation.TryGetValue(entry.ExternalKey, out var known)
                ? known
                : (Icon: "tag", Color: "#94A3B8");
            var axis = entry.ExternalKey.Split(':', 2)[0];

            tags.Add(new Tag
            {
                Name = entry.Name,
                Icon = presentation.Icon,
                Color = presentation.Color,
                Description = $"Derived by the {axis} audio classifier.",
                ExternalKey = entry.ExternalKey,
                Facet = TagFacet.Mood,
                OwnerId = SystemConfig.SystemId,
                UpdatedBy = SystemConfig.SystemId,
            });
        }

        return tags;
    }

    private static async Task<TaggingTaxonomy> LoadTaxonomyAsync(CancellationToken ct)
    {
        await using var stream = typeof(TagSeeder).Assembly.GetManifestResourceStream(TaxonomyResource)
                                 ?? throw new InvalidOperationException(
                                     $"Embedded taxonomy resource '{TaxonomyResource}' not found.");

        return await JsonSerializer.DeserializeAsync<TaggingTaxonomy>(
                   stream, JsonSerializerOptions, ct)
               ?? throw new InvalidOperationException("Failed to deserialize tagging taxonomy.");
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new(JsonSerializerDefaults.Web);

    private sealed class TaggingTaxonomy
    {
        [JsonPropertyName("version")] public string Version { get; set; } = "";

        [JsonPropertyName("genreParents")] public List<string> GenreParents { get; set; } = [];

        [JsonPropertyName("genreLabels")] public List<string> GenreLabels { get; set; } = [];

        [JsonPropertyName("moodTags")] public List<MoodTagEntry> MoodTags { get; set; } = [];
    }

    private sealed class MoodTagEntry
    {
        [JsonPropertyName("externalKey")] public string ExternalKey { get; set; } = "";

        [JsonPropertyName("name")] public string Name { get; set; } = "";
    }
}