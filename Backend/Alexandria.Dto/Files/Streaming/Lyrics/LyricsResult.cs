using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files.Streaming.Lyrics;

public record LyricsResult(
    string? PlainLyrics,
    string? SyncedLyrics,
    string? ProviderTrackId,
    bool Instrumental,
    LyricsProvider Provider,
    decimal ConfidenceScore
);