using System.ComponentModel.DataAnnotations;

namespace Alexandria.Common.Settings.Values;

public class AppearanceSettingsValue
{
    public string AccentColor { get; set; } = "amber";
    public string BackgroundColor { get; set; } = "parchment";
    public string? BackgroundImageKey { get; set; } = null;
    public DateTime? BackgroundImageUpdatedAt { get; set; } = null;

    [Range(0.1, 0.65)] public double BackgroundImageOpacity { get; set; } = 0.35;

    [Range(12, 64)] public int GridIconSize { get; set; } = 48;

    [Range(12, 64)] public int ListIconSize { get; set; } = 20;

    public bool BackgroundBlurEnabled { get; set; } = true;

    [Range(0, 24)] public int BackgroundBlurAmount { get; set; } = 8;

    public bool DisableBlurOnMobile { get; set; } = false;

    public bool TransparencyEnabled { get; set; } = true;

    [Range(10, 95)] public int SurfaceOpacity { get; set; } = 60;

    public bool ThumbnailsEnabled { get; set; } = true;
}