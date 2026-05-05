namespace Alexandria.Common.Settings.Values;

public class BehaviorSettingsValue
{
    public bool SkipDeleteConfirmation { get; set; } = false;
    public ToastLevel ToastLevel { get; set; } = ToastLevel.All;
}