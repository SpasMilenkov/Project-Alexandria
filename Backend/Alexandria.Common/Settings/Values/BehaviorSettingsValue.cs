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
}