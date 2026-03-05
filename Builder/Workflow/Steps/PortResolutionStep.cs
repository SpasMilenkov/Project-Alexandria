using Builder.Models;
using Builder.Services;
using Builder.UI;

namespace Builder.Workflow.Steps;

public class PortResolutionStep : WizardStep
{
    private readonly IPortResolver _portResolver;
    private readonly ISystemChecker _systemChecker;

    public PortResolutionStep(IPortResolver portResolver, ISystemChecker systemChecker)
    {
        _portResolver = portResolver;
        _systemChecker = systemChecker;
    }

    public override string Title => "Ports";
    public override int StepNumber => 4;

    protected override void Run(InstallationContext context)
    {
        Components.Muted("Checking host-exposed ports for conflicts...");
        Components.Spacer();

        context.PortMappings = _portResolver.ResolveAll(context.Features, context.Config.Environment, _systemChecker);

        var conflicts = context.PortMappings.Where(p => p.IsConflicted).ToList();

        if (conflicts.Count == 0)
        {
            foreach (var mapping in context.PortMappings)
                Components.CheckItem($"{mapping.ServiceName} :{mapping.DefaultPort}", true, "available");
        }
        else
        {
            // Conflict table
            Components.SectionTitle("Conflicts");

            var table = new Table()
                .Border(TableBorder.Simple)
                .BorderColor(Theme.Border)
                .AddColumn("[bold]Service[/]")
                .AddColumn("[bold]Port[/]")
                .AddColumn("[bold]Status[/]")
                .AddColumn("[bold]Process[/]")
                .Expand();

            foreach (var mapping in context.PortMappings)
            {
                var status = mapping.IsConflicted
                    ? $"[{Theme.Er}]in use[/]"
                    : $"[{Theme.Su}]ok[/]";

                table.AddRow(
                    new Markup(mapping.ServiceName.EscapeMarkup()),
                    new Markup(mapping.DefaultPort.ToString()),
                    new Markup(status),
                    new Markup($"[{Theme.Di}]{(mapping.ConflictProcess?.EscapeMarkup() ?? "-")}[/]"));
            }

            AnsiConsole.Write(new Padder(table, new Padding(2, 0, 2, 1)));

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"  [{Theme.Di}]How to resolve?[/]")
                    .HighlightStyle(Theme.AccentStyle)
                    .AddChoices(
                        "Auto-remap",
                        "Choose manually",
                        "Cancel"));

            if (choice == "Cancel")
            {
                context.ShouldAbort = true;
                return;
            }

            if (choice == "Auto-remap")
            {
                context.PortMappings = _portResolver.AutoRemap(context.PortMappings);
                if (context.PortMappings.Any(p => p.IsConflicted))
                {
                    Components.ErrorPanel("Could not find available ports. Free up ports and retry.");
                    context.ShouldAbort = true;
                    return;
                }
            }
            else
            {
                foreach (var mapping in conflicts)
                {
                    mapping.AssignedPort = AnsiConsole.Prompt(
                        new TextPrompt<int>($"  Port for {mapping.ServiceName} [{Theme.Di}](was {mapping.DefaultPort})[/]:")
                            .DefaultValue(mapping.DefaultPort + 1)
                            .Validate(port =>
                            {
                                if (port is < 1 or > 65535)
                                    return ValidationResult.Error("Invalid port");
                                if (!_systemChecker.IsPortAvailable(port))
                                    return ValidationResult.Error($"Port {port} is in use");
                                return ValidationResult.Success();
                            }));
                    mapping.IsConflicted = false;
                }
            }
        }

        // Final mapping
        Components.Spacer();
        ShowFinalMapping(context.PortMappings);

        // Store in config
        context.Config.Ports = new Dictionary<string, int>();
        context.Config.OriginalPorts = new Dictionary<string, int>();

        foreach (var mapping in context.PortMappings)
        {
            context.Config.Ports[mapping.PortKey] = mapping.AssignedPort;
            if (mapping.AssignedPort != mapping.DefaultPort)
                context.Config.OriginalPorts[mapping.PortKey] = mapping.DefaultPort;
        }
    }

    private static void ShowFinalMapping(List<PortMapping> mappings)
    {
        var table = new Table()
            .Border(TableBorder.Simple)
            .BorderColor(Theme.Border)
            .AddColumn("[bold]Service[/]")
            .AddColumn("[bold]Port[/]")
            .AddColumn("[bold]Note[/]")
            .Expand();

        foreach (var mapping in mappings)
        {
            var note = mapping.AssignedPort != mapping.DefaultPort
                ? $"[{Theme.Wa}]remapped from {mapping.DefaultPort}[/]"
                : $"[{Theme.Di}]default[/]";

            table.AddRow(
                new Markup(mapping.ServiceName.EscapeMarkup()),
                new Markup($"[{Theme.Ac}]{mapping.AssignedPort}[/]"),
                new Markup(note));
        }

        AnsiConsole.Write(new Padder(table, new Padding(2, 0, 2, 1)));
    }
}
