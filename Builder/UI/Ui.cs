using Spectre.Console;

namespace Builder.UI;

// The only place allowed to assemble styled markup. Steps call these helpers
// instead of hand-building bracket strings (locked decision D19) - the
// AccountStep crash class becomes structurally impossible.
public static class Ui
{
    public static string Dim(string text) => $"[dim]{Markup.Escape(text)}[/]";

    public static string Accent(string text) => $"[{Theme.Active.Ac}]{Markup.Escape(text)}[/]";

    public static string Success(string text) => $"[{Theme.Active.Su}]{Markup.Escape(text)}[/]";

    public static string Warning(string text) => $"[{Theme.Active.Wa}]{Markup.Escape(text)}[/]";

    public static string Error(string text) => $"[{Theme.Active.Er}]{Markup.Escape(text)}[/]";

    // A quiet parenthetical hint, e.g. inside prompt labels
    public static string Hint(string text) => $" [dim]- {Markup.Escape(text.Trim('(', ')', ' '))}[/]";

    public static void Banner()
    {
        AnsiConsole.Write(
            new FigletText("ALEXANDRIA")
                .Centered()
                .Color(Theme.Active.Accent));

        AnsiConsole.Write(
            new Rule($"[{Theme.Active.As}]{Theme.Active.DisplayName} look[/]  " +
                     Ui.Dim($"installer v0.3 - {Theme.Active.Tagline}"))
                .RuleStyle(Theme.Active.BorderStyle)
                .Centered());

        AnsiConsole.WriteLine();
    }

    public static void SectionTitle(string title)
    {
        AnsiConsole.WriteLine();

        AnsiConsole.Write(new Rule($"[bold white]{Markup.Escape(title)}[/]")
            .RuleStyle(Theme.Active.BorderStyle)
            .HeavyBorder());

        AnsiConsole.WriteLine();
    }
}
