namespace Builder.UI;

// The active theme, resolved once at startup from the saved preference and
// swapped in place when the user changes the look.
public static class Theme
{
    private static ThemeDefinition _active = Themes.Default;

    public static ThemeDefinition Active => _active;

    public static void Use(ThemeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _active = definition;
    }
}
