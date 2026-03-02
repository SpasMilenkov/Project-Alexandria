using Common.Settings.Values;

namespace API.Features.Settings.Apperarance;

public class GetAppearanceResponse
{
    public string AccentColor { get; set; } = default!;
    public string BackgroundColor { get; set; } = default!;
    public string? BackgroundImageKey { get; set; }
    public DateTime? BackgroundImageUpdatedAt { get; set; }
    public double BackgroundImageOpacity { get; set; }
    public int GridIconSize { get; set; }
    public int ListIconSize { get; set; }

    public static GetAppearanceResponse FromValue(AppearanceSettingsValue s) => new()
    {
        AccentColor = s.AccentColor,
        BackgroundColor = s.BackgroundColor,
        BackgroundImageKey = s.BackgroundImageKey,
        BackgroundImageUpdatedAt = s.BackgroundImageUpdatedAt,
        BackgroundImageOpacity = s.BackgroundImageOpacity,
        GridIconSize = s.GridIconSize,
        ListIconSize = s.ListIconSize,
    };
}
