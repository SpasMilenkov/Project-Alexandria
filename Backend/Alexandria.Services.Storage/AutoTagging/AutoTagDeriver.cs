using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Autotag;

namespace Alexandria.Services.Storage.AutoTagging;

/// <summary>
/// Pure, side-effect-free derivation of auto-tag candidates from enrichment payloads.
/// Maps model-emitted keys to taxonomy tags via <see cref="Tag.ExternalKey"/> (never by
/// parsing display names); unknown keys are reported on the result, not logged here.
/// </summary>
public static class AutoTagDeriver
{
    /// <summary>
    /// Derives genre candidates: top-N predictions by score, floor at
    /// <c>Genre.ConfidenceThreshold</c>, each label looked up by <see cref="Tag.ExternalKey"/>
    /// (the full discogs <c>Parent---Child</c> label). Matched children become candidates;
    /// each parent with a matched child gets a rollup candidate with the max child score.
    /// </summary>
    public static TagDerivationResult DeriveGenre(
        EssentiaGenrePayload payload,
        IReadOnlyDictionary<string, Tag> taxonomy,
        GenreTaggingOptions options)
    {
        var unknown = new List<string>();
        var matched = new List<(Tag Tag, double Score)>();

        foreach (var prediction in payload.Predictions
                     .Where(p => !string.IsNullOrWhiteSpace(p.Label))
                     .OrderByDescending(p => p.Score)
                     .ThenBy(p => p.Label, StringComparer.Ordinal)
                     .Take(options.TopN))
        {
            if (prediction.Score < options.ConfidenceThreshold) continue;

            var label = prediction.Label!;
            if (!taxonomy.TryGetValue(label, out var tag))
            {
                unknown.Add(label);
                continue;
            }

            matched.Add((tag, prediction.Score));
        }

        var candidates = matched
            .Select(m => new TagCandidate(m.Tag.Id, m.Score, TagFacet.Genre))
            .ToList();

        candidates.AddRange(matched
            .Where(m => m.Tag.ParentId.HasValue)
            .GroupBy(m => m.Tag.ParentId!.Value)
            .Select(g => new TagCandidate(g.Key, g.Max(m => m.Score), TagFacet.Genre)));

        return new TagDerivationResult(candidates, unknown);
    }

    /// <summary>
    /// Derives mood candidates: per axis the winning pole (max score, ties broken
    /// deterministically by pole name); the composite <c>{axis}:{pole}</c> key is looked
    /// up by <see cref="Tag.ExternalKey"/>. Axes whose winner is below
    /// <c>Mood.ConfidenceThreshold</c> (including near-50/50 splits) tag neither pole.
    /// </summary>
    public static TagDerivationResult DeriveMood(
        IReadOnlyDictionary<string, Dictionary<string, double>> payload,
        IReadOnlyDictionary<string, Tag> taxonomy,
        MoodTaggingOptions options)
    {
        var unknown = new List<string>();
        var candidates = new List<TagCandidate>();

        foreach (var (axis, scores) in payload.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (scores.Count == 0) continue;

            var winner = scores
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .First();

            if (winner.Value < options.ConfidenceThreshold) continue;

            var key = $"{axis}:{winner.Key}";
            if (!taxonomy.TryGetValue(key, out var tag))
            {
                unknown.Add(key);
                continue;
            }

            candidates.Add(new TagCandidate(tag.Id, winner.Value, TagFacet.Mood));
        }

        return new TagDerivationResult(candidates, unknown);
    }
}