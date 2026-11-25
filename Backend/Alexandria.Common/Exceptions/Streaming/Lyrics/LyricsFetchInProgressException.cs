namespace Alexandria.Common.Exceptions.Streaming.Lyrics;

public class LyricsFetchInProgressException(Guid lyricsId)
    : Exception($"Lyrics {lyricsId} are still being fetched; try again once the current fetch finishes.");
