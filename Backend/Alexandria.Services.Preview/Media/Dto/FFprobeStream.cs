using System.Text.Json.Serialization;

namespace Alexandria.Services.Preview.Media.Dto;

/// <summary>
/// Individual stream information from ffprobe
/// </summary>
public class FFprobeStream
{
    [JsonPropertyName("codec_name")] public string? CodecName { get; set; }

    [JsonPropertyName("codec_type")] public string? CodecType { get; set; }

    [JsonPropertyName("width")] public int Width { get; set; }

    [JsonPropertyName("height")] public int Height { get; set; }

    [JsonPropertyName("disposition")] public FFprobeDisposition? Disposition { get; set; }
}