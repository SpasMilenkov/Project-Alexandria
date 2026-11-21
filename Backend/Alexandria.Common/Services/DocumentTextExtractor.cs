namespace Alexandria.Common.Services;

public static class DocumentTextExtractor
{
    /// <summary>
    /// Raise this when extraction changes, and the sweep reprocesses every older row.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// About 2,500 pages of prose. Postgres would store more, but nobody searches past this, and
    /// every stored character costs space in each backup.
    /// </summary>
    public const int MaxStoredCharacters = 5_000_000;

    /// <summary>
    /// Postgres rejects a search vector over 1 MB. Prose stays far below that at any length,
    /// because positions cap at 256 per word, but text made of unique tokens (IDs, hashes) hit it
    /// near 1 M characters on Postgres 16. This prefix measured 614 KB in that worst case.
    /// </summary>
    public const int FallbackIndexedCharacters = 500_000;
}