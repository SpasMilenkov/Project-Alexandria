using Builder.UI;

namespace Builder.Workflow.Steps;

public class SuccessStep : WizardStep
{
    public override string Title => "Done";
    public override int StepNumber => 8;

    protected override void Run(InstallationContext context)
    {
        var frontendPort = context.Config.Ports.GetValueOrDefault("FRONTEND_PORT", 80);
        var urls = $"[bold white]Frontend[/]    [{Theme.Ac}]http://localhost:{frontendPort}[/]";

        if (context.Features.Monitoring)
        {
            var grafanaPort = context.Config.Ports.GetValueOrDefault("GRAFANA_PORT", 3000);
            urls += $"\n[bold white]Grafana[/]     [{Theme.Ac}]http://localhost:{grafanaPort}[/]";
        }

        if (context.IsLocalPreview)
        {
            var pgPort = context.Config.Ports.GetValueOrDefault("POSTGRES_PORT", 5432);
            var rmqPort = context.Config.Ports.GetValueOrDefault("RABBITMQ_MANAGEMENT_PORT", 15672);
            urls += $"\n[bold white]PostgreSQL[/]  [{Theme.Ac}]localhost:{pgPort}[/]";
            urls += $"\n[bold white]RabbitMQ[/]    [{Theme.Ac}]http://localhost:{rmqPort}[/]";
        }

        Components.SuccessPanel("Installation Complete", urls);

        // Credentials
        Components.SectionTitle("Credentials");

        var credTable = new Table()
            .Border(TableBorder.Simple)
            .BorderColor(Theme.Border)
            .AddColumn(new TableColumn("[bold]Service[/]").PadRight(2))
            .AddColumn(new TableColumn("[bold]Value[/]"))
            .Expand();

        var creds = new (string Label, string Key)[]
        {
            ("DB Password", "DB_PASSWORD"),
            ("RabbitMQ User", "RABBITMQ_USER"),
            ("RabbitMQ Pass", "RABBITMQ_PASSWORD"),
            ("JWT Secret", "JWT_SECRET"),
            ("S3 Access Key", "GARAGE_S3_ACCESS_KEY"),
            ("S3 Secret Key", "GARAGE_S3_SECRET_KEY"),
        };

        if (context.Features.Monitoring)
            creds = [.. creds, ("Grafana Pass", "GRAFANA_ADMIN_PASSWORD")];

        foreach (var (label, key) in creds)
        {
            if (context.Config.Credentials.TryGetValue(key, out var value))
            {
                credTable.AddRow(
                    new Markup($"[{Theme.Ac}]{label}[/]"),
                    new Markup($"[{Theme.Di}]{value.EscapeMarkup()}[/]"));
            }
        }

        AnsiConsole.Write(new Padder(credTable, new Padding(2, 0, 2, 0)));
        Components.Spacer();

        // Remapped ports
        if (context.Config.OriginalPorts.Count > 0)
        {
            Components.SectionTitle("Remapped Ports");
            foreach (var mapping in context.PortMappings.Where(m => m.AssignedPort != m.DefaultPort))
            {
                AnsiConsole.MarkupLine(
                    $"    [{Theme.Ac}]{mapping.ServiceName}[/]  {mapping.DefaultPort} [{Theme.Wa}]→[/] {mapping.AssignedPort}");
            }
            Components.Spacer();
        }

        // Config file
        Components.Muted($"Config: {Path.Combine(context.InstallPath, "alexandria-config.json")}");
        Components.Spacer();

        // Commands
        Components.SectionTitle("Commands");
        var cd = context.InstallPath.EscapeMarkup();
        AnsiConsole.MarkupLine($"    [{Theme.Di}]$[/] cd {cd} && docker compose logs -f");
        AnsiConsole.MarkupLine($"    [{Theme.Di}]$[/] cd {cd} && docker compose restart");
        AnsiConsole.MarkupLine($"    [{Theme.Di}]$[/] cd {cd} && docker compose down");
        Components.Spacer();
    }
}
