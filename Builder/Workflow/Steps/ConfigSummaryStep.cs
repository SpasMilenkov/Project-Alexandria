using Builder.Services;
using Builder.UI;

namespace Builder.Workflow.Steps;

public class ConfigSummaryStep : WizardStep
{
    private readonly ICredentialService _credentialService;
    private readonly IResourceCalculator _resourceCalculator;

    public ConfigSummaryStep(ICredentialService credentialService, IResourceCalculator resourceCalculator)
    {
        _credentialService = credentialService;
        _resourceCalculator = resourceCalculator;
    }

    public override string Title => "Summary";
    public override int StepNumber => 5;

    protected override void Run(InstallationContext context)
    {
        // Generate credentials
        context.Config.Credentials = _credentialService.GenerateAllCredentials(context.Features);

        var minimumRam = _resourceCalculator.CalculateMinimumRamMb(context.Features);
        var mode = context.IsLocalPreview ? "Local Preview" : "Production";

        // Service count
        var serviceCount = 8; // postgres, garage, garage-init, rabbitmq, api, doc-worker, frontend, backup
        if (context.Features.MediaProcessing) serviceCount += 1;
        if (context.Features.Monitoring) serviceCount += 2;

        // Config table
        Components.SectionTitle("Configuration");

        var configTable = new Table()
            .Border(TableBorder.Simple)
            .BorderColor(Theme.Border)
            .AddColumn(new TableColumn("[bold]Setting[/]").PadRight(2))
            .AddColumn(new TableColumn("[bold]Value[/]"))
            .Expand();

        configTable.AddRow(new Markup($"[{Theme.Ac}]Mode[/]"), new Markup(mode));
        configTable.AddRow(new Markup($"[{Theme.Ac}]S3 Provider[/]"), new Markup("Garage"));
        configTable.AddRow(new Markup($"[{Theme.Ac}]Media Processing[/]"),
            new Markup(context.Features.MediaProcessing ? $"[{Theme.Su}]on[/]" : $"[{Theme.Di}]off[/]"));
        configTable.AddRow(new Markup($"[{Theme.Ac}]Monitoring[/]"),
            new Markup(context.Features.Monitoring ? $"[{Theme.Su}]on[/]" : $"[{Theme.Di}]off[/]"));
        configTable.AddRow(new Markup($"[{Theme.Ac}]Services[/]"), new Markup(serviceCount.ToString()));
        configTable.AddRow(new Markup($"[{Theme.Ac}]Est. RAM[/]"), new Markup($"{minimumRam} MB"));

        if (context.Resources.TotalMemoryMb > 0)
            configTable.AddRow(new Markup($"[{Theme.Ac}]System RAM[/]"), new Markup($"{context.Resources.TotalMemoryMb} MB"));

        AnsiConsole.Write(new Padder(configTable, new Padding(2, 0, 2, 0)));

        // Credentials
        Components.SectionTitle("Credentials");

        var credTable = new Table()
            .Border(TableBorder.Simple)
            .BorderColor(Theme.Border)
            .AddColumn(new TableColumn("[bold]Name[/]").PadRight(2))
            .AddColumn(new TableColumn("[bold]Value[/]"))
            .Expand();

        foreach (var (key, value) in context.Config.Credentials)
        {
            var masked = value.Length > 8
                ? value[..4] + new string('·', value.Length - 8) + value[^4..]
                : new string('·', value.Length);

            credTable.AddRow(
                new Markup($"[{Theme.Ac}]{key.EscapeMarkup()}[/]"),
                new Markup($"[{Theme.Di}]{masked.EscapeMarkup()}[/]"));
        }

        AnsiConsole.Write(new Padder(credTable, new Padding(2, 0, 2, 0)));
        Components.Muted("Full credentials saved to alexandria-config.json after install.");
        Components.Spacer();

        // Confirm
        if (!AnsiConsole.Confirm("  Proceed with installation?"))
        {
            context.ShouldAbort = true;
        }
    }
}
