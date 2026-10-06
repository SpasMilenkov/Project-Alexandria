namespace Alexandria.Data.Models.Enumerators;

/// <summary>
/// Curated Wrapped card kinds, in deck priority order (D11). Serialized as numbers.
/// </summary>
public enum WrappedCardType
{
    TopArtists = 0,
    TopSongs = 1,
    ListeningTime = 2,
    Persona = 3,
    Bookends = 4,
    MostReplayed = 5,
    MostMinutes = 6,
    Streak = 7,
    BusiestDay = 8,
    LongestSitting = 9,
    Discoveries = 10,
    NewArtists = 11,
    LoyalListener = 12,
    Exploration = 13,
    RetainedDiscovery = 14,
    Chapters = 15,
    ReturningFavorite = 16,
}