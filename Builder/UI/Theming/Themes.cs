using Spectre.Console;

namespace Builder.UI.Theming;

// The shipped visual identities. Mint is the factory default and matches the
// web app's default look; Papyrus and Aurora stay as alternatives.
public static class Themes
{
    public static readonly ThemeDefinition Mint = new()
    {
        Id = "mint",
        DisplayName = "Mint",
        Tagline = "emerald on cream - fresh ink, quiet shelves",
        Accent = new Color(52, 211, 153),
        AccentSecondary = new Color(167, 243, 208),
        SuccessColor = Color.Green3_1,
        WarningColor = Color.DarkOrange,
        ErrorColor = Color.IndianRed1,
        MutedColor = Color.Grey62,
        BorderColor = Color.Grey37,
        WaterFill = new Color(14, 52, 48),
        WaterColor = new Color(45, 212, 191),
        ChartColors =
        [
            new Color(52, 211, 153),
            new Color(167, 243, 208),
            Color.SkyBlue1,
            Color.Tan,
            Color.Plum2,
            Color.LightSalmon1,
        ],
    };

    public static readonly ThemeDefinition Papyrus = new()
    {
        Id = "papyrus",
        DisplayName = "Papyrus",
        Tagline = "warm gold on slate - old library, new shelves",
        Accent = Color.Gold1,
        AccentSecondary = Color.Tan,
        SuccessColor = Color.Green3_1,
        WarningColor = Color.DarkOrange,
        ErrorColor = Color.IndianRed1,
        MutedColor = Color.Grey62,
        BorderColor = Color.Grey37,
        WaterFill = new Color(16, 52, 62),
        WaterColor = Color.CadetBlue,
        // Six distinct colors, one per feature that brings its own services;
        // grey stays free for the core that always runs
        ChartColors =
        [
            Color.Gold1,
            Color.Tan,
            Color.SandyBrown,
            Color.DarkGoldenrod,
            Color.PaleGreen1,
            Color.CadetBlue,
        ],
    };

    public static readonly ThemeDefinition Aurora = new()
    {
        Id = "aurora",
        DisplayName = "Aurora",
        Tagline = "violet and ice - modern console nights",
        Accent = Color.MediumPurple2,
        AccentSecondary = Color.SteelBlue1,
        SuccessColor = Color.Green3_1,
        WarningColor = Color.DarkOrange,
        ErrorColor = Color.IndianRed1,
        MutedColor = Color.Grey62,
        BorderColor = Color.Grey37,
        WaterFill = new Color(22, 30, 70),
        WaterColor = Color.SteelBlue1,
        ChartColors =
        [
            Color.MediumPurple2,
            Color.SteelBlue1,
            Color.Plum2,
            Color.SkyBlue1,
            Color.PaleGreen1,
            Color.LightSalmon1,
        ],
    };

    public static readonly IReadOnlyList<ThemeDefinition> All = [Mint, Papyrus, Aurora];

    public static ThemeDefinition Default => Mint;

    public static ThemeDefinition Resolve(string? id)
    {
        return All.FirstOrDefault(t =>
            string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Default;
    }

    public static ThemeDefinition After(ThemeDefinition current)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (ReferenceEquals(All[i], current))
            {
                return All[(i + 1) % All.Count];
            }
        }

        return Default;
    }

    public static int IndexOf(ThemeDefinition theme)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (ReferenceEquals(All[i], theme))
            {
                return i;
            }
        }

        return 0;
    }
}
