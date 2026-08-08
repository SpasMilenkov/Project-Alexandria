namespace Alexandria.Data.Models.Enumerators;

/// <summary>
/// Derivation family a tag belongs to. Null for user-created tags; populated only
/// for system-seeded taxonomy tags. Used to scope sync operations to a facet
/// (e.g. pruning the genre candidate set must not touch mood tags) and by the
/// frontend for genre/mood filtering.
/// </summary>
public enum TagFacet
{
    /// <summary>Genre taxonomy (top-level genres and their subgenres).</summary>
    Genre = 0,

    /// <summary>Mood/voice axis tags.</summary>
    Mood = 1,
}