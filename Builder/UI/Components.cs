using Spectre.Console;

namespace Builder.UI;

// Legacy-facing component kit. New code prefers Ui helpers; these wrappers
// remain so existing call sites stay stable during the migration phases.
public static class Components
{
    public static void StepHeader(int step, int total, string title)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [{Theme.Di}]{step}/{total}[/]  [bold white]{title}[/]");
        AnsiConsole.WriteLine();
    }

    public static void Banner() => Ui.Banner();

    public static void WarningPanel(string message)
    {
        var panel = new Panel(new Markup($"[{Theme.Wa}]{message.EscapeMarkup()}[/]"))
        {
            Header = new PanelHeader($"[{Theme.Wa}]![/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = Theme.Active.BorderStyle,
            Padding = new Padding(2, 1),    
            Expand = true,
        };

        AnsiConsole.Write(new Padder(panel, new Padding(2, 0, 2, 1)));
    }

    public static void ErrorPanel(string message)
    {
        var panel = new Panel(new Markup($"[{Theme.Er}]{message.EscapeMarkup()}[/]"))
        {
            Header = new PanelHeader($"[{Theme.Er}]Error[/]"),
            Border = BoxBorder.Heavy,
            BorderStyle = Theme.Active.ErrorStyle,
            Padding = new Padding(2, 1),
            Expand = true,
        };

        AnsiConsole.Write(new Padder(panel, new Padding(2, 0, 2, 1)));
    }

    public static void SuccessPanel(string title, string content)
    {
        var panel = new Panel(new Markup(content))
        {
            Header = new PanelHeader($"[{Theme.Su}]◆[/] [{Theme.Su}]{title}[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = Theme.Active.BorderStyle,
            Padding = new Padding(2, 1),
            Expand = true,
        };

        AnsiConsole.Write(new Padder(panel, new Padding(2, 0, 2, 1)));
    }

    public static void InfoPanel(string title, string content)
    {
        var panel = new Panel(new Markup(content))
        {
            Header = new PanelHeader($"[{Theme.Ac}]{title}[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = Theme.Active.BorderStyle,
            Padding = new Padding(2, 1),
            Expand = true,
        };

        AnsiConsole.Write(new Padder(panel, new Padding(2, 0, 2, 1)));
    }

    public static void CheckItem(string label, bool success, string? detail = null)
    {
        var icon = success ? $"[{Theme.Su}]◆[/]" : $"[{Theme.Er}]✗[/]";
        var detailText = detail != null ? $" [{Theme.Di}]{detail.EscapeMarkup()}[/]" : "";
        AnsiConsole.MarkupLine($"    {icon} {label}{detailText}");
    }

    public static void Spacer()
    {
        AnsiConsole.WriteLine();
    }

    public static void SectionTitle(string title) => Ui.SectionTitle(title);

    public static void Muted(string text) => AnsiConsole.MarkupLine($"  {Ui.Dim(text)}");
}
