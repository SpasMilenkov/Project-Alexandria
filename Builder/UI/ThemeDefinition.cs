namespace Builder.UI;

// A complete visual identity. Steps never touch raw colors - they read the
// active definition through the Theme facade or (better) Ui helpers.
public sealed class ThemeDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Tagline { get; init; }

    public required Spectre.Console.Color Accent { get; init; }
    public required Spectre.Console.Color AccentSecondary { get; init; }
    public required Spectre.Console.Color SuccessColor { get; init; }
    public required Spectre.Console.Color WarningColor { get; init; }
    public required Spectre.Console.Color ErrorColor { get; init; }
    public required Spectre.Console.Color DimColor { get; init; }
    public required Spectre.Console.Color BorderColor { get; init; }
    public required IReadOnlyList<Spectre.Console.Color> ChartColors { get; init; }

    public Spectre.Console.Style AccentStyle => new(Accent);
    public Spectre.Console.Style AccentSecondaryStyle => new(AccentSecondary);
    public Spectre.Console.Style DimStyle => new(DimColor);
    public Spectre.Console.Style BorderStyle => new(BorderColor);
    public Spectre.Console.Style SuccessStyle => new(SuccessColor);
    public Spectre.Console.Style WarningStyle => new(WarningColor);
    public Spectre.Console.Style ErrorStyle => new(ErrorColor);

    // Markup fragments for inline string building (prefer Ui helpers over
    // hand-assembling brackets)
    public string Ac => Accent.ToMarkup();
    public string As => AccentSecondary.ToMarkup();
    public string Di => DimColor.ToMarkup();
    public string Su => SuccessColor.ToMarkup();
    public string Wa => WarningColor.ToMarkup();
    public string Er => ErrorColor.ToMarkup();
}
