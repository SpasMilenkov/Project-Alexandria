namespace Builder.UI;

public static class Theme
{
    // Core palette — muted base with selective accents
    public static readonly Color Fg = Color.Grey;
    public static readonly Color FgBright = Color.White;
    public static readonly Color Accent = Color.Teal;
    public static readonly Color Success = Color.Green3_1;
    public static readonly Color Warning = Color.DarkOrange;
    public static readonly Color Error = Color.Red3_1;
    public static readonly Color Dim = Color.Grey42;
    public static readonly Color Border = Color.Grey35;

    // Reusable styles
    public static readonly Style AccentStyle = new(Accent);
    public static readonly Style DimStyle = new(Dim);
    public static readonly Style BorderStyle = new(Border);
    public static readonly Style SuccessStyle = new(Success);
    public static readonly Style WarningStyle = new(Warning);
    public static readonly Style ErrorStyle = new(Error);

    // Markup helpers
    public static string Ac => Accent.ToMarkup();
    public static string Di => Dim.ToMarkup();
    public static string Su => Success.ToMarkup();
    public static string Wa => Warning.ToMarkup();
    public static string Er => Error.ToMarkup();
    public static string Bo => Border.ToMarkup();
}
