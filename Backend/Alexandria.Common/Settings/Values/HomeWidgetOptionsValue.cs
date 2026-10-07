using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Settings.Values;

public class HomeWidgetOptionsValue
{
    public string TimeZone { get; set; } = "local";
    public HomeShortcutGroup ShortcutGroup { get; set; } = HomeShortcutGroup.Files;
}