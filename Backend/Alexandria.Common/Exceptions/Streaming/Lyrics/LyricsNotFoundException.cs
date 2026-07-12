namespace Alexandria.Common.Exceptions.Streaming.Lyrics;

public class LyricsNotFoundException(Guid id) : Exception($"Lyrics for song with {id} not found");