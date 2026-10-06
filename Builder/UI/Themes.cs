namespace Builder.UI;

// The two shipped visual identities (locked decision D24). Papyrus is the
// factory default; Aurora is the cool alternative.
public static class Themes
{
    public static readonly ThemeDefinition Papyrus = new()
    {
        Id = "papyrus",
        DisplayName = "Papyrus",
        Tagline = "warm gold on slate - old library, new shelves",
        Accent = Spectre.Console.Color.Gold1,
        AccentSecondary = Spectre.Console.Color.Tan,
        SuccessColor = Spectre.Console.Color.Green3_1,
        WarningColor = Spectre.Console.Color.DarkOrange,
        ErrorColor = Spectre.Console.Color.Red3_1,
        // TODO(maintainer): muted text is near-invisible on dark terminal themes
        // (reported on Catppuccin). Brighten manually - candidates Grey66/70/74
        // need a one-line compile probe on stable 0.57.2 before committing.
        DimColor = Spectre.Console.Color.Grey58,
        BorderColor = Spectre.Console.Color.Grey35,
        ChartColors =
        [
            Spectre.Console.Color.Gold1,
            Spectre.Console.Color.Tan,
            Spectre.Console.Color.SandyBrown,
            Spectre.Console.Color.DarkGoldenrod,
            Spectre.Console.Color.PaleGreen1,
            Spectre.Console.Color.Grey62,
        ],
    };

    public static readonly ThemeDefinition Aurora = new()
    {
        Id = "aurora",
        DisplayName = "Aurora",
        Tagline = "violet and ice - modern console nights",
        Accent = Spectre.Console.Color.MediumPurple2,
        AccentSecondary = Spectre.Console.Color.SteelBlue1,
        SuccessColor = Spectre.Console.Color.Green3_1,
        WarningColor = Spectre.Console.Color.DarkOrange,
        ErrorColor = Spectre.Console.Color.Red3_1,
        // TODO(maintainer): same dark-theme visibility issue as Papyrus -
        // brighten manually (see note above).
        DimColor = Spectre.Console.Color.Grey58,
        BorderColor = Spectre.Console.Color.Grey35,
        ChartColors =
        [
            Spectre.Console.Color.MediumPurple2,
            Spectre.Console.Color.SteelBlue1,
            Spectre.Console.Color.Plum2,
            Spectre.Console.Color.SkyBlue1,
            Spectre.Console.Color.PaleGreen1,
            Spectre.Console.Color.Grey62,
        ],
    };

    public static readonly IReadOnlyList<ThemeDefinition> All = [Papyrus, Aurora];

    public static ThemeDefinition Default => Papyrus;

    public static ThemeDefinition Resolve(string? id)
    {
        return All.FirstOrDefault(t =>
            string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Default;
    }
}
