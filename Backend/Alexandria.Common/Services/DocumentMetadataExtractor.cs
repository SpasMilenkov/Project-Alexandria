namespace Alexandria.Common.Services;

public static class DocumentMetadataExtractor
{
    /// <summary>
    /// Raise this when extraction changes, and the backfill reprocesses every older row.
    /// </summary>
    public const int CurrentVersion = 1;
}
