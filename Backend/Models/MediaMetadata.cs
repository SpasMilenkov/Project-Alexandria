namespace Models;

/// <summary>
/// Stores metadata for media files such as video and audio files
/// Required since they have more metadata than a typical .txt or .docx file
/// </summary>
public class MediaMetadata : IBase
{
    //Primary key
    public Guid Id { get; set; }
    
    // File information
    public double Duration { get; set; }
    public double BitrateMbps { get; set; }
    public string? FormatName { get; set; }
    public string? ThumbnailPath { get; set; }
    
    // Stream information
    public string? VideoCodec { get; set; }
    public string? AudioCodec { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool HasAudio { get; set; }
    
    // Audio metadata tags (ID3, etc.)
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Year { get; set; }
    public string? Genre { get; set; }
    
    //Datetime properties
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    
    //Navigation properties
    public Guid FileId { get; set; }
    public File? File { get; set; }
    
}