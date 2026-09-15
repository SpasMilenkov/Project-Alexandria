namespace Alexandria.Common.Settings.Values;

public class BehaviorSettingsValue
{
    public bool SkipDeleteConfirmation { get; set; } = false;
    public ToastLevel ToastLevel { get; set; } = ToastLevel.All;

    /// <summary>
    /// When <c>false</c> (default), an auto-tag re-run keeps the higher confidence already
    /// stored even if the model now scores the same tag lower. When <c>true</c>, the latest
    /// (possibly lower) verdict is taken. Never affects user-removed tags.
    /// </summary>
    public bool AllowAutoTagRegression { get; set; } = false;

    /// <summary>
    /// When <c>false</c> (default), the automated media pipeline never overwrites a
    /// non-empty descriptive metadata field (Title, Artist, Album, Year, Genre), so user
    /// corrections survive re-runs. When <c>true</c>, incoming automated output always wins.
    /// Technical fields (duration, codecs, dimensions) are never gated.
    /// </summary>
    public bool AllowAutomaticMetadataOverwrite { get; set; } = false;

    public const int MinAutoPlaylistTracks = 1;
    public const int MaxAutoPlaylistTracks = 100;

    /// <summary>
    /// Minimum streamable tracks for the reconciler to create a tag or genre
    /// auto-playlist (default 10). Vocabulary-driven kinds explode into singletons
    /// without this; artist/album playlists always materialize. Only gates creation:
    /// existing playlists keep syncing below the threshold.
    /// </summary>
    public int AutoPlaylistMinTracks { get; set; } = 10;
}