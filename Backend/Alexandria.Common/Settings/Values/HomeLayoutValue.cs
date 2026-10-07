namespace Alexandria.Common.Settings.Values;

public class HomeLayoutValue
{
    public int Columns { get; set; } = 12;
    public List<HomeWidgetValue> Widgets { get; set; } = [];
}