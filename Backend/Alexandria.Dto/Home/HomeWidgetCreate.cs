using System.Text.Json;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Dto.Home;

public sealed class HomeWidgetCreate
{
    public Guid Id { get; set; }
    public HomeLayout Layout { get; set; }
    public HomeWidgetType Type { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool? ShowTitle { get; set; }
    public int OptionsVersion { get; set; } = 1;
    public JsonElement Options { get; set; }
}
