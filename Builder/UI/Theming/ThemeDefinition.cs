using Spectre.Console;

namespace Builder.UI.Theming;

// A complete visual identity. Screens never touch raw colors; they read the
// active definition through Theme.Active or the Ui markup helpers.
public sealed class ThemeDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Tagline { get; init; }

    public required Color Accent { get; init; }
    public required Color AccentSecondary { get; init; }
    public required Color SuccessColor { get; init; }
    public required Color WarningColor { get; init; }
    public required Color ErrorColor { get; init; }

    // Muted text uses a real color instead of the terminal's faint attribute,
    // which several dark palettes render close to invisible.
    public required Color MutedColor { get; init; }
    public required Color BorderColor { get; init; }
    public required IReadOnlyList<Color> ChartColors { get; init; }

    // The splash map's water: a filled background so the coastline reads as
    // a shape, with wave marks drawn over it
    public required Color WaterFill { get; init; }
    public required Color WaterColor { get; init; }

    public Style AccentStyle => new(Accent);
    public Style BorderStyle => new(BorderColor);
    public Style MutedStyle => new(MutedColor);
    public Style ErrorStyle => new(ErrorColor);

    public string Ac => Accent.ToMarkup();
    public string As => AccentSecondary.ToMarkup();
    public string Mu => MutedColor.ToMarkup();
    public string Bo => BorderColor.ToMarkup();
    public string Su => SuccessColor.ToMarkup();
    public string Wa => WarningColor.ToMarkup();
    public string Er => ErrorColor.ToMarkup();
}
