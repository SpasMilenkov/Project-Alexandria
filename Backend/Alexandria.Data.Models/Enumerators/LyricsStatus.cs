namespace Alexandria.Data.Models.Enumerators;

public enum LyricsStatus
{
    PendingFetch,
    Fetching,
    Fetched,
    FetchFailed,
    NoMatch
}