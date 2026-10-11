using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

public class HomeWidget : IBase
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public HomeLayout Layout { get; set; }
    public HomeWidgetType Type { get; set; }

    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool? ShowTitle { get; set; }

    public int OptionsVersion { get; set; } = 1;
    public string OptionsJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
