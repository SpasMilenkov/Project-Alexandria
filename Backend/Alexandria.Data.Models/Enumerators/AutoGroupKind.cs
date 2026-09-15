namespace Alexandria.Data.Models.Enumerators;

/// <summary>
/// What an auto-maintained playlist is grouped by. Each kind uses its own typed key
/// columns on <see cref="Playlist"/> only the columns
/// relevant to the kind are set.
/// </summary>
public enum AutoGroupKind
{
    Artist = 0,
    Album = 1,
    Tag = 2,
    Genre = 3,
    ParentGenre = 4,
    Decade = 5,
}