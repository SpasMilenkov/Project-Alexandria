using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files.Streaming.Lyrics;

public class LyricsDto
{
    public Guid Id { get; set; }
    public LyricsStatus Status { get; set; }
    public LyricsProvider Provider { get; set; }
    public bool Cached { get; set; }

    public decimal? ConfidenceScore { get; set; }
    public DateTime? FetchedAt { get; set; }
    public string? PlayLyrics { get; set; }
    public string? SyncedLyrics { get; set; }
}