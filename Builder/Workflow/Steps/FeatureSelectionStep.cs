using Builder.UI;

namespace Builder.Workflow.Steps;

public class FeatureSelectionStep : WizardStep
{
    public override string Title => "Features";
    public override int StepNumber => 2;

    protected override void Run(InstallationContext context)
    {
        Components.Muted("Choose which optional features to include.");
        Components.Spacer();

        // Media processing
        context.Features.MediaProcessing = AnsiConsole.Prompt(
            new TextPrompt<bool>($"  Enable media processing? [{Theme.Di}](audio/video)[/]")
                .AddChoice(true)
                .AddChoice(false)
                .DefaultValue(true)
                .WithConverter(b => b ? "yes" : "no"));

        // Monitoring
        context.Features.Monitoring = AnsiConsole.Prompt(
            new TextPrompt<bool>($"  Enable monitoring? [{Theme.Di}](Prometheus + Grafana)[/]")
                .AddChoice(true)
                .AddChoice(false)
                .DefaultValue(false)
                .WithConverter(b => b ? "yes" : "no"));

        Components.Spacer();

        // Frontend port
        var defaultFrontendPort = context.IsLocalPreview ? 3000 : 80;
        context.Features.FrontendPort = AnsiConsole.Prompt(
            new TextPrompt<int>($"  Frontend port [{Theme.Di}](default {defaultFrontendPort})[/]:")
                .DefaultValue(defaultFrontendPort)
                .Validate(port => port is >= 1 and <= 65535
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Port must be between 1 and 65535")));

        // Grafana port
        if (context.Features.Monitoring)
        {
            var defaultGrafana = context.Features.FrontendPort == 3000 ? 3001 : 3000;
            context.Features.GrafanaPort = AnsiConsole.Prompt(
                new TextPrompt<int>($"  Grafana port [{Theme.Di}](default {defaultGrafana})[/]:")
                    .DefaultValue(defaultGrafana)
                    .Validate(port => port is >= 1 and <= 65535
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Port must be between 1 and 65535")));
        }

        // Local preview: debug ports
        if (context.IsLocalPreview)
        {
            Components.Spacer();
            Components.SectionTitle("Debug Ports");

            context.Features.PostgresPort = AnsiConsole.Prompt(
                new TextPrompt<int>($"  PostgreSQL port [{Theme.Di}](default 5432)[/]:")
                    .DefaultValue(5432)
                    .Validate(port => port is >= 1 and <= 65535
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Port must be between 1 and 65535")));

            context.Features.RabbitmqManagementPort = AnsiConsole.Prompt(
                new TextPrompt<int>($"  RabbitMQ Management port [{Theme.Di}](default 15672)[/]:")
                    .DefaultValue(15672)
                    .Validate(port => port is >= 1 and <= 65535
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Port must be between 1 and 65535")));
        }

        context.Config.Features = context.Features;
        Components.Spacer();
    }
}
