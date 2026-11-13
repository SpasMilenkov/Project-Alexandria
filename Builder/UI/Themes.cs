using Spectre.Console;

namespace Builder.UI;

// The two shipped visual identities. Papyrus is the factory default; Aurora
// is the cool alternative.
public static class Themes
{
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

    public static readonly IReadOnlyList<ThemeDefinition> All = [Papyrus, Aurora];

    public static ThemeDefinition Default => Papyrus;

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
