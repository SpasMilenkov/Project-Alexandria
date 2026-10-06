using Builder.Models;
using Builder.Services;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class PortsScreen
{
    // D5 copy: lowest possible tech barrier, no metaphors
    private const string PortExplainer =
        "Programs on your computer talk to each other through numbered slots " +
        "called ports. Alexandria needs three free ones: two for the website " +
        "(one regular, one secure) and one for the monitoring dashboard if you " +
        "enabled it. If another program already uses a number, we simply pick " +
        "a different one.";

    public static ScreenOutcome Run(
        Shell shell,
        IPortResolver resolver,
        ISystemChecker checker)
    {
        var context = shell.Context;
        var mappings = resolver.ResolveAll(context.Features, checker);

        context.PortMappings = mappings;

        IRenderable Table()
        {
            var grid = new Grid().AddColumn().AddColumn().AddColumn();

            foreach (var m in mappings)
            {
                var port = m.AssignedPort != m.DefaultPort
                    ? $"[{Theme.Active.Wa}]{m.DefaultPort} → {m.AssignedPort}[/]"
                    : $":{m.AssignedPort}";

                var status = m.IsConflicted
                    ? $"[{Theme.Active.Er}]in use[/]"
                    : $"[{Theme.Active.Su}]free[/]";

                grid.AddRow(
                    new Markup($"[bold]{m.ServiceName}[/]"),
                    new Markup(port),
                    new Markup(status));
            }

            return grid;
        }

        IRenderable Header(string note) => new Rows(
            Views.Title("Ports"),
            Views.Note(PortExplainer),
            Views.Note(note),
            Table());

        var conflicts = mappings.Where(m => m.IsConflicted).ToList();

        if (conflicts.Count == 0)
        {
            shell.SetMain(new Rows(
                Header("All ports are free."),
                new Rule().RuleStyle(Theme.Active.BorderStyle),
                Views.Note("Press Enter to continue - Esc takes you back.")));

            if (IntroScreen.WaitEnter(shell))
            {
                return ScreenOutcome.Back;
            }
        }
        else
        {
            var resolution = Widgets.AskSingleSelect(
                shell,
                Header($"{conflicts.Count} port(s) are busy"),
                "How should we handle the busy ports?",
                ["Pick different ports for me", "I will type free ports", "Cancel setup"],
                s => s);

            // Esc on the resolution menu goes back a screen (D38)
            if (resolution is null)
            {
                return ScreenOutcome.Back;
            }

            switch (resolution)
            {
                case "Pick different ports for me":
                    resolver.AutoRemap(mappings);

                    if (mappings.Any(m => m.IsConflicted))
                    {
                        shell.Context.ShouldAbort = true;

                        return ScreenOutcome.Abort;
                    }

                    break;

                case "I will type free ports":
                    foreach (var conflict in conflicts)
                    {
                        if (!TryManualPort(shell, conflict, checker, () => Header("Type a free port")))
                        {
                            return ScreenOutcome.Back;
                        }

                        conflict.IsConflicted = false;
                    }

                    break;

                default:
                    context.ShouldAbort = true;

                    return ScreenOutcome.Abort;
            }

            // Show the final assignment so "pick for me" is never a leap of
            // faith, and Esc still offers a way back
            shell.SetMain(new Rows(
                Header("Here are the ports Alexandria will use."),
                new Rule().RuleStyle(Theme.Active.BorderStyle),
                Views.Note("Press Enter to continue - Esc takes you back.")));

            if (IntroScreen.WaitEnter(shell))
            {
                return ScreenOutcome.Back;
            }
        }

        context.Config.Ports = mappings.ToDictionary(m => m.PortKey, m => m.AssignedPort);

        context.Config.OriginalPorts = mappings
            .Where(m => m.AssignedPort != m.DefaultPort)
            .ToDictionary(m => m.PortKey, m => m.DefaultPort);

        return ScreenOutcome.Continue;
    }

    // Returns false when the user pressed Esc to go back (D38)
    private static bool TryManualPort(Shell shell, PortMapping mapping, ISystemChecker checker, Func<IRenderable> header)
    {
        var raw = Widgets.AskText(
            shell,
            header(),
            new TextFieldSpec
            {
                Label = $"Port for {mapping.ServiceName} (was {mapping.DefaultPort})",
                Initial = (mapping.DefaultPort + 1).ToString(),
                Hint = "1-65535",
                Validator = value =>
                {
                    if (!int.TryParse(value, out var port) || port is < 1 or > 65535)
                    {
                        return "Enter a number between 1 and 65535";
                    }

                    return checker.IsPortAvailable(port)
                        ? null
                        : $"Port {port} is already in use";
                },
            });

        if (shell.TakeEscape())
        {
            return false;
        }

        mapping.AssignedPort = int.Parse(raw);

        return true;
    }
}
