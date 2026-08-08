using System.Text.Json.Serialization;

namespace Alexandria.Workers.MediaMetadata.Messages;

/// <summary>
/// Per-batch completion message published by the Python worker
/// (<c>{"batch_id","backbone","ok_count","fail_count","output_dir","seconds","model_versions"}</c>).
/// </summary>
public sealed class CompletionMessage
{
    [JsonPropertyName("batch_id")] public Guid BatchId { get; set; }

    [JsonPropertyName("backbone")] public string? Backbone { get; set; }

    [JsonPropertyName("ok_count")] public int OkCount { get; set; }

    [JsonPropertyName("fail_count")] public int FailCount { get; set; }

    [JsonPropertyName("output_dir")] public string? OutputDir { get; set; }

    [JsonPropertyName("seconds")] public double Seconds { get; set; }

    [JsonPropertyName("model_versions")] public ModelVersions? ModelVersions { get; set; }
}

/// <summary>
/// Per-file result JSON written by the Python worker into <c>output_dir/{fileId}.json</c>.
/// </summary>
public sealed class PerFileOutput
{
    [JsonPropertyName("input_file")] public string? InputFile { get; set; }

    [JsonPropertyName("backbone")] public string? Backbone { get; set; }

    [JsonPropertyName("success")] public bool Success { get; set; }

    [JsonPropertyName("error")] public string? Error { get; set; }

    [JsonPropertyName("processing_seconds")]
    public double? ProcessingSeconds { get; set; }

    [JsonPropertyName("genre")] public GenreResult? Genre { get; set; }

    [JsonPropertyName("mood")] public Dictionary<string, Dictionary<string, double>>? Mood { get; set; }

    [JsonPropertyName("model_versions")] public ModelVersions? ModelVersions { get; set; }
}

public sealed class GenreResult
{
    [JsonPropertyName("backbone")] public string? Backbone { get; set; }

    [JsonPropertyName("predictions")] public List<GenrePrediction> Predictions { get; set; } = [];
}

public sealed class GenrePrediction
{
    [JsonPropertyName("label")] public string? Label { get; set; }

    [JsonPropertyName("score")] public double Score { get; set; }
}

public sealed class ModelVersions
{
    [JsonPropertyName("genre")] public string? Genre { get; set; }

    [JsonPropertyName("mood")] public string? Mood { get; set; }
}