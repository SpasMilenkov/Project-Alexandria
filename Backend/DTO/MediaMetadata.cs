namespace DTO;

/// <summary>
/// Metadata extracted from media files
/// </summary>
public class MediaMetadata
{
    // File information
    public double Duration { get; set; }
    public long FileSize { get; set; }
    public double BitrateMbps { get; set; }
    public string? FormatName { get; set; }
    
    // Stream information
    public string? VideoCodec { get; set; }
    public string? AudioCodec { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool HasVideo { get; set; }
    public bool HasAudio { get; set; }
    public bool HasEmbeddedArtwork { get; set; }
    
    // Audio metadata tags (ID3, etc.)
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Year { get; set; }
    public string? Genre { get; set; }
}