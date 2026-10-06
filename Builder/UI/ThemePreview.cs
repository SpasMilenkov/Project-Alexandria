using Spectre.Console;

namespace Builder.UI;

// Draws a small self-describing sample of a palette so the user can judge it
// before committing. Uses the candidate definition's own styles explicitly -
// not the active theme - since the whole point is comparison.
public static class ThemePreview
{
    public static void Render(ThemeDefinition definition)
    {
        var content = new Markup(string.Join("\n",
            $"[{definition.Ac}]◆ Accent - headers, selections, key values[/]",
            $"[{definition.As}]◇ Secondary - descriptions and soft emphasis[/]",
            $"[{definition.Su}]✓ Success - everything is running[/]",
            $"[{definition.Wa}]! Warning - close to the limits[/]",
            $"[{definition.Di}]· Dim text - quiet details and hints[/]"));

        var panel = new Panel(content)
        {
            Header = new PanelHeader(
                $"[{definition.Ac}]{definition.DisplayName}[/] [{definition.Di}]- {definition.Tagline}[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = definition.BorderStyle,
            Padding = new Padding(2, 1),
            Expand = true,
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }
}
