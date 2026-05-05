using System.Text.Json.Serialization;

namespace Alexandria.Services.Preview.Media.Dto;

/// <summary>
/// Stream disposition flags (e.g., attached_pic for album artwork)
/// </summary>
public class FFprobeDisposition
{
    [JsonPropertyName("attached_pic")] public int AttachedPic { get; set; }
}