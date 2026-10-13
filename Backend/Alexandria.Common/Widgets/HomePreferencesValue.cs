namespace Alexandria.Common.Widgets;

public sealed class HomePreferencesValue
{
    public bool Initialized { get; set; }
    public bool HasMobileLayout { get; set; }
    public bool UseSeparateMobileLayout { get; set; }
    public bool ShowWidgetTitles { get; set; }
    public bool ShowHeaderGreeting { get; set; } = true;
}
