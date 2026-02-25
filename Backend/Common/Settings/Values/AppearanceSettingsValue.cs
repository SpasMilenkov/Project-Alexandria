using System.ComponentModel.DataAnnotations;

namespace Common.Settings.Values;

public class AppearanceSettingsValue
{
    public string AccentColor { get; set; } = "amber";
    public string BackgroundColor { get; set; } = "parchment";
    public string? BackgroundImageKey { get; set; } = null;
    public DateTime? BackgroundImageUpdatedAt { get; set; } = null;

    [Range(0.1, 0.65)]
    public double BackgroundImageOpacity { get; set; } = 0.35;

    [Range(12, 64)]
    public int GridIconSize { get; set; } = 48;

    [Range(12, 64)]
    public int ListIconSize { get; set; } = 20;
}
