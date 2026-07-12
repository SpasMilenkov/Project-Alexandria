namespace Alexandria.Dto.Files.Streaming.Lyrics;

/// <summary>
/// Response that is coming from LRCLIB. Comes with 2 formats of lyrics
/// [mm:ss.xx] timestamps for the synced and plain lyrics
/// When the music piece is instrumental both should be null
/// and the instrumental bool should be set to true
/// </summary>
public sealed record LrcLibResponse(
    int Id,
    string TrackName,
    string ArtistName,
    string? AlbumName,
    double Duration,
    bool Instrumental,
    string? PlainLyrics,
    string? SyncedLyrics,
    string? LyricsFile = null
);