using System.Text.Json.Serialization;

namespace Alexandria.Workers.MediaMetadata.Messages;

/// <summary>
/// Batch dispatch payload consumed by the Python classifier workers.
/// Serialized with snake_case keys to match the Python contract:
/// <c>{"batch_id","output_dir","files"}</c>.
/// </summary>
public sealed class ClassifyRequest
{
    [JsonPropertyName("batch_id")] public Guid BatchId { get; set; }

    [JsonPropertyName("output_dir")] public required string OutputDir { get; set; }

    [JsonPropertyName("files")] public required string[] Files { get; set; }
}