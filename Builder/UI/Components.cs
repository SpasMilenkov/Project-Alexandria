namespace Builder.UI;

public static class Components
{
    public static void StepHeader(int step, int total, string title)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [{Theme.Di}]{step}/{total}[/]  [bold white]{title}[/]");
        AnsiConsole.WriteLine();
    }

    public static void Banner()
    {
        AnsiConsole.WriteLine();
        var panel = new Panel(
            new Markup($"[bold {Theme.Ac}]A L E X A N D R I A[/]\n[{Theme.Di}]Installer v1.0[/]"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Theme.BorderStyle,
            Padding = new Padding(2, 1),
            Width = 40,
        };
        AnsiConsole.Write(new Padder(panel, new Padding(2, 1, 0, 0)));
    }

    public static void WarningPanel(string message)
    {
        var panel = new Panel(new Markup($"[{Theme.Wa}]{message.EscapeMarkup()}[/]"))
        {
            Header = new PanelHeader($"[{Theme.Wa}]![/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = Theme.BorderStyle,
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
            BorderStyle = Theme.ErrorStyle,
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
            BorderStyle = Theme.BorderStyle,
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
            BorderStyle = Theme.BorderStyle,
            Padding = new Padding(2, 1),
            Expand = true,
        };
        AnsiConsole.Write(new Padder(panel, new Padding(2, 0, 2, 1)));
    }

    public static Table KeyValueTable(Dictionary<string, string> items)
    {
        var table = new Table()
            .Border(TableBorder.Simple)
            .BorderColor(Theme.Border)
            .AddColumn(new TableColumn("[bold]Setting[/]").PadRight(2))
            .AddColumn(new TableColumn("[bold]Value[/]"))
            .Expand();

        foreach (var (key, value) in items)
        {
            table.AddRow(
                new Markup($"[{Theme.Ac}]{key.EscapeMarkup()}[/]"),
                new Markup(value));
        }

        return table;
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

    public static void SectionTitle(string title)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [bold white]{title}[/]");
        AnsiConsole.WriteLine();
    }

    public static void Muted(string text)
    {
        AnsiConsole.MarkupLine($"  [{Theme.Di}]{text.EscapeMarkup()}[/]");
    }
}
