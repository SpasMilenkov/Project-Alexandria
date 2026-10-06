namespace Builder.UI;

// Facade over the active theme. Resolved once at startup from the persisted
// preference (locked decision D28); the legacy token properties (Di, Ac, ...)
// keep every existing call site compiling while migration to Ui helpers
// proceeds phase by phase.
public static class Theme
{
    private static ThemeDefinition _active = Themes.Default;

    public static ThemeDefinition Active => _active;

    public static void Use(ThemeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _active = definition;
    }

    public static string Ac => _active.Ac;
    public static string As => _active.As;
    public static string Di => _active.Di;
    public static string Su => _active.Su;
    public static string Wa => _active.Wa;
    public static string Er => _active.Er;

    public static Spectre.Console.Style AccentStyle => _active.AccentStyle;
    public static Spectre.Console.Style DimStyle => _active.DimStyle;
    public static Spectre.Console.Style BorderStyle => _active.BorderStyle;
    public static Spectre.Console.Style SuccessStyle => _active.SuccessStyle;
    public static Spectre.Console.Style WarningStyle => _active.WarningStyle;
    public static Spectre.Console.Style ErrorStyle => _active.ErrorStyle;

    // Legacy color accessors still referenced by pre-migration call sites
    public static Spectre.Console.Color Border => _active.BorderColor;
}
