using Builder.Models;
using Builder.Workflow;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell;

// Builds the right pane: where the user is in the wizard, what their choices
// cost, and which look is active. Sections are dropped lowest priority first
// when the terminal is too short to show them all.
internal static class SidebarView
{
    public static IRenderable Build(
        SidebarModel model,
        InstallationContext context,
        IReadOnlyList<string> steps,
        int stepIndex,
        int width,
        int rows)
    {
        var look = LookSection(model);
        var budget = rows - Height(look, width);
        var sections = new List<IRenderable>();

        var rail = StepRail(steps, stepIndex, Math.Max(1, budget));

        if (rail is not null)
        {
            sections.Add(rail);
            budget -= Height(rail, width);
        }

        foreach (var optional in new[] { Stats(model), Features(context, width) })
        {
            if (optional is null)
            {
                continue;
            }

            var cost = Height(optional, width) + 1;

            if (cost > budget)
            {
                break;
            }

            sections.Add(optional);
            budget -= cost;
        }

        var parts = new List<IRenderable>();

        foreach (var section in sections)
        {
            if (parts.Count > 0)
            {
                parts.Add(Divider());
            }

            parts.Add(section);
        }

        var used = parts.Sum(part => Height(part, width));

        // Pin the look and theme hint to the bottom of the pane
        for (var i = used + Height(look, width); i < rows; i++)
        {
            parts.Add(new Text(string.Empty));
        }

        parts.Add(look);

        return new Rows(parts);
    }

    private static IRenderable? StepRail(IReadOnlyList<string> steps, int stepIndex, int budget)
    {
        if (steps.Count == 0)
        {
            return null;
        }

        // Keep the current step visible when the pane cannot list them all
        var visible = Math.Min(steps.Count, budget);
        var first = Math.Clamp(stepIndex - 1 - visible / 2, 0, steps.Count - visible);
        var lines = new List<string>();

        for (var i = first; i < first + visible; i++)
        {
            var number = i + 1;

            if (number < stepIndex)
            {
                lines.Add($"{Ui.Success("✓")} {Ui.Muted(steps[i])}");
            }
            else if (number == stepIndex)
            {
                lines.Add($"[bold {Theme.Active.Ac}]❯ {Markup.Escape(steps[i])}[/]");
            }
            else
            {
                lines.Add($"{Ui.Muted("·")} {Markup.Escape(steps[i])}");
            }
        }

        return new Markup(string.Join("\n", lines));
    }

    private static IRenderable? Stats(SidebarModel model)
    {
        if (model.Stats.Count == 0)
        {
            return null;
        }

        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(2))
            .AddColumn(new GridColumn().NoWrap());

        foreach (var stat in model.Stats)
        {
            grid.AddRow(new Markup(Ui.Muted(stat.Label)), new Markup(StatValue(stat)));
        }

        return grid;
    }

    private static string StatValue(SidebarStat stat)
    {
        var value = Markup.Escape(stat.Value);

        return stat.Tone switch
        {
            StatTone.Error => $"[bold {Theme.Active.Er}]{value}[/]",
            StatTone.Warning => $"[bold {Theme.Active.Wa}]{value}[/]",
            _ => $"[bold]{value}[/]",
        };
    }

    private static IRenderable? Features(InstallationContext context, int width)
    {
        if (!context.FeaturesSelected)
        {
            return null;
        }

        var lines = FeatureCatalog.All.Select(feature =>
        {
            var label = Truncate(feature.Label, width - 2);

            return context.Features.IsEnabled(feature.Id)
                ? $"{Ui.Success("●")} {Markup.Escape(label)}"
                : $"{Ui.Muted("○")} {Ui.Muted(label)}";
        });

        return new Markup(string.Join("\n", lines));
    }

    private static IRenderable LookSection(SidebarModel model)
    {
        var active = Theme.Active;
        var swatches = string.Join(" ", new[] { active.Ac, active.As, active.Su, active.Wa, active.Er }
            .Select(color => $"[{color}]■[/]"));

        var rows = new List<IRenderable>
        {
            Divider(),
            new Markup($"[{active.Ac}]{Markup.Escape(active.DisplayName)}[/] {Ui.Muted("look")}  {swatches}"),
        };

        if (model.SaveWarning is not null)
        {
            rows.Add(new Markup(Ui.Warning("! " + model.SaveWarning)));
        }

        return new Rows(rows);
    }

    private static IRenderable Divider()
    {
        return new Rule().RuleStyle(Theme.Active.BorderStyle);
    }

    private static int Height(IRenderable renderable, int width)
    {
        return SegmentLines.Render(renderable, width).Count;
    }

    private static string Truncate(string label, int max)
    {
        return label.Length <= max ? label : label[..(max - 1)] + "…";
    }
}
