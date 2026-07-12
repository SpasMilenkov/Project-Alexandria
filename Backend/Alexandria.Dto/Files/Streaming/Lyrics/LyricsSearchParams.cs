namespace Alexandria.Dto.Files.Streaming.Lyrics;

/// <summary>
/// The params that are used for searching lyrics for a song
/// All nullable except the name itself. because they are derived
/// from the audio file's tags and that is most often unreliable
/// </summary>
public class LyricsSearchParams
{
    public required string Name { get; set; }
    public string? AlbumName { get; set; }
    public string? Artist { get; set; }
    public int? Duration { get; set; }
}