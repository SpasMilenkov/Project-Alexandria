using System.Text.Json;
using Alexandria.Common;
using Alexandria.Data.Models;
using Alexandria.Dto.Autotag;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Storage.AutoTagging;

/// <summary>
/// Derives auto-tag candidates from enrichment rows. Loads the system-seeded taxonomy
/// (all tags with an <see cref="Tag.ExternalKey"/> and <see cref="Tag.Facet"/>), matches
/// model-emitted keys to tags, and logs a drift warning for keys that have no tag.
/// </summary>
public partial class AutoTagDerivationService(
    IUnitOfWork unitOfWork,
    IOptions<AutoTaggingOptions> options,
    ILogger<AutoTagDerivationService> logger) : IAutoTagDerivationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<TagCandidate>> DeriveAsync(
        IReadOnlyCollection<FileEnrichment> enrichmentRows,
        CancellationToken ct = default)
    {
        if (enrichmentRows.Count == 0) return [];

        var taxonomyTags = (await unitOfWork.Tags
                .FindAsync(t => t.ExternalKey != null && t.Facet != null, ct))
            .ToList();
        var taxonomy = taxonomyTags.ToDictionary(t => t.ExternalKey!);

        var candidates = new List<TagCandidate>();

        foreach (var row in enrichmentRows)
        {
            TagDerivationResult result;
            if (row.Analyzer.StartsWith("essentia-genre", StringComparison.OrdinalIgnoreCase))
            {
                result = DeriveGenreRow(row, taxonomy);
            }
            else if (row.Analyzer.Equals("essentia-mood", StringComparison.OrdinalIgnoreCase))
            {
                result = DeriveMoodRow(row, taxonomy);
            }
            else
            {
                continue;
            }

            foreach (var key in result.UnknownKeys)
            {
                LogDrift(logger, key, row.FileId, row.Analyzer, row.Version);
            }

            candidates.AddRange(result.Candidates);
        }

        return candidates
            .GroupBy(c => c.TagId)
            .Select(g => g.MaxBy(c => c.Confidence)!)
            .ToList();
    }

    private TagDerivationResult DeriveGenreRow(
        FileEnrichment row,
        IReadOnlyDictionary<string, Tag> taxonomy)
    {
        var payload = JsonSerializer.Deserialize<EssentiaGenrePayload>(row.PayloadJson, JsonOptions);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Backbone)) return Empty;

        return AutoTagDeriver.DeriveGenre(payload, taxonomy, options.Value.Genre);
    }

    private TagDerivationResult DeriveMoodRow(
        FileEnrichment row,
        IReadOnlyDictionary<string, Tag> taxonomy)
    {
        var payload = TryDeserializeMoodPayload(row.PayloadJson);
        if (payload is null) return Empty;

        return AutoTagDeriver.DeriveMood(payload, taxonomy, options.Value.Mood);
    }

    /// <summary>
    /// Reads the mood axis dictionary, tolerating failure rows written as
    /// <c>{"success":false,...}</c> (returned as null → skipped).
    /// </summary>
    private static IReadOnlyDictionary<string, Dictionary<string, double>>? TryDeserializeMoodPayload(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
        if (document.RootElement.TryGetProperty("success", out var success)
            && success.ValueKind == JsonValueKind.False) return null;

        try
        {
            return document.RootElement.Deserialize<Dictionary<string, Dictionary<string, double>>>(JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly TagDerivationResult Empty =
        new([], []);
}