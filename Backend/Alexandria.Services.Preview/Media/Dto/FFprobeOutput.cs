using System.Text.Json.Serialization;

namespace Alexandria.Services.Preview.Media.Dto;

/// <summary>
/// FFprobe JSON output structure
/// </summary>
public class FFprobeOutput
{
    [JsonPropertyName("streams")] public List<FFprobeStream>? Streams { get; set; }

    [JsonPropertyName("format")] public FFprobeFormat? Format { get; set; }
}