using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Settings.Values;

public class HomeWidgetValue
{
    public string InstanceId { get; set; } = string.Empty;
    public HomeWidgetType Type { get; set; }
    public HomeWidgetSize Size { get; set; } = HomeWidgetSize.Medium;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 6;
    public int Height { get; set; } = 4;
    public HomeWidgetOptionsValue Options { get; set; } = new();
}