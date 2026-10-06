using Alexandria.Data.Models.Enumerators;
using System.Diagnostics;
using System.Text.Json;
using Alexandria.Dto.Files.Streaming.Stats;

namespace Alexandria.Services.Streaming.Wrapped;

/// <summary>Loads and validates the bundled Wrapped duration catalog.</summary>
internal static class DurationCatalogLoader
{
    private const string ResourceName = "Alexandria.Wrapped.DurationDataset.json";

    private static readonly Dictionary<string, WrappedDurationCategory> Categories = new(StringComparer.Ordinal)
    {
        ["sport"] = WrappedDurationCategory.Sport,
        ["movie"] = WrappedDurationCategory.Movie,
        ["historical_event"] = WrappedDurationCategory.HistoricalEvent,
        ["movie_series"] = WrappedDurationCategory.MovieSeries,
        ["tv"] = WrappedDurationCategory.Television,
        ["everyday"] = WrappedDurationCategory.Everyday,
        ["travel"] = WrappedDurationCategory.Travel,
        ["time"] = WrappedDurationCategory.Time,
        ["space"] = WrappedDurationCategory.Space,
        ["animation"] = WrappedDurationCategory.Animation,
        ["theatre"] = WrappedDurationCategory.Theatre,
        ["audiobook"] = WrappedDurationCategory.Audiobook
    };

    private static readonly HashSet<string> SourceTiers =
        ["<2h", "2-3h", "3-6h", "6-12h", "12-20h", "20-40h", "40-80h", "80-120h", "120h+"];

    public static WrappedDurationCategory CategoryFor(string key) => Categories[key];

    public static IReadOnlyList<DurationAnchor> Load()
    {
        try
        {
            var catalog = ReadCatalog();

            Validate(catalog);

            // Both IDs describe the same 80-hour milestone; keep one canonical entry.
            return catalog.Anchors.Where(a => a.Id != "ten_workdays").ToList();
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidOperationException
                                              or ArgumentException)
        {
            Trace.TraceError($"Wrapped duration catalog unavailable: {exception.Message}");

            return [];
        }
    }

    private static DurationCatalog ReadCatalog()
    {
        using var stream = typeof(DurationCatalogLoader).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException("Wrapped duration catalog resource is missing.");

        return JsonSerializer.Deserialize<DurationCatalog>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
               ?? throw new InvalidOperationException("Wrapped duration catalog is empty.");
    }

    private static void Validate(DurationCatalog catalog)
    {
        if (catalog.Anchors is not { Count: > 0 })
            throw new InvalidOperationException("Wrapped duration catalog has no anchors.");

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var anchor in catalog.Anchors)
        {
            if (anchor == null)
                throw new InvalidOperationException("Wrapped duration catalog contains a null anchor.");

            if (!HasValidFields(anchor, ids))
                throw new InvalidOperationException($"Invalid Wrapped duration anchor: {anchor.Id}");

            if (!HasValidRange(anchor))
                throw new InvalidOperationException($"Invalid Wrapped duration range: {anchor.Id}");
        }
    }

    private static bool HasValidFields(DurationAnchor anchor, HashSet<string> ids)
    {
        return !string.IsNullOrWhiteSpace(anchor.Id) && ids.Add(anchor.Id)
                                                     && !string.IsNullOrWhiteSpace(anchor.Name)
                                                     && !string.IsNullOrWhiteSpace(anchor.DurationLabel)
                                                     && !string.IsNullOrWhiteSpace(anchor.Copy)
                                                     && Categories.ContainsKey(anchor.Category)
                                                     && SourceTiers.Contains(anchor.Tier)
                                                     && double.IsFinite(anchor.DurationMinutes)
                                                     && anchor.DurationMinutes > 0;
    }

    private static bool HasValidRange(DurationAnchor anchor)
    {
        if (anchor.DurationRangeMinutes is not { } range) return true;

        return range.Length == 2
               && double.IsFinite(range[0]) && double.IsFinite(range[1])
               && range[0] > 0 && range[0] <= anchor.DurationMinutes && range[1] >= anchor.DurationMinutes;
    }
}
