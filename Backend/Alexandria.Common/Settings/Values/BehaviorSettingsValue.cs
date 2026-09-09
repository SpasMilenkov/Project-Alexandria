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
}