using System.Text.Json.Serialization;

namespace Alexandria.Services.Storage.AutoTagging;

/// <summary>
/// Genre prediction payload stored in <see cref="Alexandria.Data.Models.FileEnrichment.PayloadJson"/>
/// for <c>essentia-genre-*</c> analyzers. Shape mirrors the worker's <c>GenreResult</c>:
/// <c>{"backbone":"effnet","predictions":[{"label":"Rock---Nu Metal","score":0.42}]}</c>.
/// A failure row serialized as <c>{"success":false,...}</c> deserializes to an empty
/// payload (no backbone, no predictions) and is skipped by the derivation service.
/// </summary>
public sealed class EssentiaGenrePayload
{
    [JsonPropertyName("backbone")] public string? Backbone { get; set; }

    [JsonPropertyName("predictions")] public List<EssentiaGenrePrediction> Predictions { get; set; } = [];
}

public sealed class EssentiaGenrePrediction
{
    [JsonPropertyName("label")] public string? Label { get; set; }

    [JsonPropertyName("score")] public double Score { get; set; }
}