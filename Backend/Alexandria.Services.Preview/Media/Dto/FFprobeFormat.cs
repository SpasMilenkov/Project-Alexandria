using System.Text.Json.Serialization;

namespace Alexandria.Services.Preview.Media.Dto;

/// <summary>
/// Format/container information from ffprobe
/// </summary>
public class FFprobeFormat
{
    [JsonPropertyName("format_name")] public string? FormatName { get; set; }

    [JsonPropertyName("duration")] public string? Duration { get; set; }

    [JsonPropertyName("size")] public string? Size { get; set; }

    [JsonPropertyName("bit_rate")] public string? BitRate { get; set; }

    [JsonPropertyName("tags")] public Dictionary<string, string>? Tags { get; set; }
}