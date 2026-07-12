using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

/// <summary>
/// The lyrics of a song, fetched by either an external service (such as LRCLIB) or imported manually by the user
/// Used in the media streaming part of the project. 
/// </summary>
public class TrackLyrics : IBase
{
    public Guid Id { get; set; }
    
    public string? PlainLyrics { get; set; }
    public string? SyncedLyrics { get; set; }
    public bool IsInstrumental { get; set; }
    
    public LyricsProvider SourceProvider { get; set; }
    public bool Cached { get; set; }
    public string? ProviderTrackId { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public LyricsStatus Status { get; set; }
    public DateTime? FetchedAt { get; set; }
    
    //Navigation

    public Guid TranspilationJobId { get; set; }
    public TranspilationJob? TranspilationJob { get; set; }
    
    // Base
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}