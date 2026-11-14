using Spectre.Console;

namespace Builder.UI.Theming;

// Escaping markup helpers. Screens build styled text through these instead of
// concatenating user values into bracket markup by hand.
public static class Ui
{
    public static string Muted(string text) => $"[{Theme.Active.Mu}]{Markup.Escape(text)}[/]";

    public static string Accent(string text) => $"[{Theme.Active.Ac}]{Markup.Escape(text)}[/]";

    public static string Strong(string text) => $"[bold]{Markup.Escape(text)}[/]";

    public static string Success(string text) => $"[{Theme.Active.Su}]{Markup.Escape(text)}[/]";

    public static string Warning(string text) => $"[{Theme.Active.Wa}]{Markup.Escape(text)}[/]";

    public static string Error(string text) => $"[{Theme.Active.Er}]{Markup.Escape(text)}[/]";
}
