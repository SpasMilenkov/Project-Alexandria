namespace Alexandria.Data.Models.Enumerators;

/// <summary>
/// What an <c>alexandria://&lt;kind&gt;/&lt;id&gt;</c> link in a note points at. A note is a file, so
/// links between notes use <see cref="File"/>.
/// </summary>
public enum LinkTargetKind
{
    File = 1,
    Folder = 2,
    Tag = 3,
    Playlist = 4,
}
